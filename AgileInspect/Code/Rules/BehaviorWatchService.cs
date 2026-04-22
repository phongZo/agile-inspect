using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgileInspect.Code.Rules
{
    public sealed class BehaviorWatchService
    {
        private sealed class WatchSession
        {
            public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
            public string RuleId { get; init; } = string.Empty;
            public DateTime StartedAtUtc { get; init; }
            public DateTime ExpiresAtUtc { get; set; }
            public int ExtendCount { get; set; }
            public List<Condition> LastConditionGroup { get; set; } = new();
            public Timer? Timer { get; set; }
        }

        private sealed class RuleOverview
        {
            public string ruleId { get; set; } = string.Empty;
            public string ruleDescription { get; set; } = string.Empty;
            public int started { get; set; }
            public int extended { get; set; }
            public int finished { get; set; }
            public int resolved { get; set; }
            public int deferred { get; set; }
            public int ignored { get; set; }
        }

        private sealed class WatchOverview
        {
            public DateTime generatedAtUtc { get; set; } = DateTime.UtcNow;
            public RuleOverview totals { get; set; } = new RuleOverview();
            public Dictionary<string, RuleOverview> byRule { get; set; } = new Dictionary<string, RuleOverview>(StringComparer.OrdinalIgnoreCase);
        }

        private readonly object _sync = new();
        private readonly Dictionary<string, WatchSession> _sessionsByRuleId = new(StringComparer.OrdinalIgnoreCase);

        private readonly string _appDataDir;
        private readonly string _sessionLogPath;
        private readonly string _overviewPath;
        private readonly object _queueFileLock = new();

        private static readonly Lazy<BehaviorWatchService> _lazy = new(() => new BehaviorWatchService());
        public static BehaviorWatchService Instance => _lazy.Value;

        private BehaviorWatchService()
        {
            _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
            Directory.CreateDirectory(_appDataDir);
            _sessionLogPath = Path.Combine(_appDataDir, "behavior_watch_sessions.jsonl");
            _overviewPath = Path.Combine(_appDataDir, "behavior_watch_overview.json");
        }

        public void OnRuleMatched(string ruleId, List<Condition> conditionGroup)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return;
            }

            var cfg = StoreCfgJson.Instance?.interventionWatcherConfig;
            if (cfg == null || !cfg.enable)
            {
                return;
            }

            int watchWindowSec = Math.Max(1, cfg.defaultWatchWindowSec);
            TimeSpan watchWindow = TimeSpan.FromSeconds(watchWindowSec);

            lock (_sync)
            {
                if (_sessionsByRuleId.TryGetValue(ruleId, out var existing))
                {
                    existing.ExpiresAtUtc = DateTime.UtcNow.Add(watchWindow);
                    existing.ExtendCount += 1;
                    existing.LastConditionGroup = conditionGroup?.Select(c => c).ToList() ?? new List<Condition>();
                    existing.Timer?.Change(watchWindow, Timeout.InfiniteTimeSpan);
                    WriteSessionLog("watch_extended", existing, null);
                    UpdateOverview(
                        ruleId,
                        BuildConditionSummary(existing.LastConditionGroup),
                        startedDelta: 0,
                        extendedDelta: 1,
                        finishedDelta: 0,
                        resolvedDelta: 0,
                        deferredDelta: 0,
                        ignoredDelta: 0);
                    return;
                }

                var session = new WatchSession
                {
                    RuleId = ruleId,
                    StartedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.Add(watchWindow),
                    ExtendCount = 0,
                    LastConditionGroup = conditionGroup?.Select(c => c).ToList() ?? new List<Condition>()
                };

                session.Timer = new Timer(_ => FinalizeWatch(ruleId), null, watchWindow, Timeout.InfiniteTimeSpan);
                _sessionsByRuleId[ruleId] = session;

                WriteSessionLog("watch_started", session, null);
                UpdateOverview(
                    ruleId,
                    BuildConditionSummary(session.LastConditionGroup),
                    startedDelta: 1,
                    extendedDelta: 0,
                    finishedDelta: 0,
                    resolvedDelta: 0,
                    deferredDelta: 0,
                    ignoredDelta: 0);
            }
        }

        private void FinalizeWatch(string ruleId)
        {
            WatchSession? session;
            lock (_sync)
            {
                if (!_sessionsByRuleId.TryGetValue(ruleId, out session))
                {
                    return;
                }

                var remaining = session.ExpiresAtUtc - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    session.Timer?.Change(remaining, Timeout.InfiniteTimeSpan);
                    return;
                }

                _sessionsByRuleId.Remove(ruleId);
            }

            session.Timer?.Dispose();

            bool stillMatched = RuleService.IsRuleCurrentlyMatched(ruleId);
            string outcome = stillMatched
                ? "ignored"
                : (session.ExtendCount > 0 ? "deferred" : "resolved");

            var extra = new JObject
            {
                ["outcome"] = outcome,
                ["durationSec"] = Math.Max(0, (DateTime.UtcNow - session.StartedAtUtc).TotalSeconds)
            };

            WriteSessionLog("watch_finished", session, extra);
            UpdateOverview(
                ruleId,
                BuildConditionSummary(session.LastConditionGroup),
                startedDelta: 0,
                extendedDelta: 0,
                finishedDelta: 1,
                resolvedDelta: outcome == "resolved" ? 1 : 0,
                deferredDelta: outcome == "deferred" ? 1 : 0,
                ignoredDelta: outcome == "ignored" ? 1 : 0);
        }

        private void WriteSessionLog(string type, WatchSession session, JObject? extra)
        {
            try
            {
                var logObj = new JObject
                {
                    ["timestampUtc"] = DateTime.UtcNow,
                    ["type"] = type,
                    ["sessionId"] = session.SessionId,
                    ["ruleId"] = session.RuleId,
                    ["conditionSummary"] = BuildConditionSummary(session.LastConditionGroup),
                    ["conditions"] = JArray.FromObject(session.LastConditionGroup),
                    ["startedAtUtc"] = session.StartedAtUtc,
                    ["expiresAtUtc"] = session.ExpiresAtUtc,
                    ["extendCount"] = session.ExtendCount
                };

                if (extra != null)
                {
                    foreach (var p in extra.Properties())
                    {
                        logObj[p.Name] = p.Value;
                    }
                }

                EnqueueSessionLine(logObj);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[BehaviorWatch] Failed to write session log: {ex.Message}");
            }
        }

        /// <summary>FIFO queue file: one JSON object per line. Drained by <see cref="SendSessionQueueAsync"/>.</summary>
        private void EnqueueSessionLine(JObject logObj)
        {
            logObj["queueId"] = Guid.NewGuid().ToString("N");
            var line = logObj.ToString(Formatting.None) + Environment.NewLine;
            lock (_queueFileLock)
            {
                File.AppendAllText(_sessionLogPath, line);
            }
        }

        /// <summary>POST pending lines from <c>behavior_watch_sessions.jsonl</c> in order; stop on first failure.</summary>
        public async Task SendSessionQueueAsync()
        {
            if (string.Equals(StoreCfgJson.Instance.deployType, "serverless", StringComparison.OrdinalIgnoreCase))
                return;

            List<(string queueId, string line)> snapshot;
            lock (_queueFileLock)
            {
                if (!File.Exists(_sessionLogPath))
                    return;

                var text = File.ReadAllText(_sessionLogPath);
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                snapshot = new List<(string, string)>();
                foreach (var raw in lines)
                {
                    try
                    {
                        var o = JObject.Parse(raw);
                        var qid = (string)o["queueId"];
                        if (string.IsNullOrWhiteSpace(qid))
                            continue;
                        snapshot.Add((qid, raw));
                    }
                    catch
                    {
                        // skip malformed line
                    }
                }
            }

            if (snapshot.Count == 0)
                return;

            var sentOk = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (qid, line) in snapshot)
            {
                var envelope = BuildBehaviorWatchEnvelope(line);
                if (!await TryPostEventLogAsync(envelope).ConfigureAwait(false))
                    break;
                sentOk.Add(qid);
            }

            if (sentOk.Count == 0)
                return;

            lock (_queueFileLock)
            {
                if (!File.Exists(_sessionLogPath))
                    return;

                var text = File.ReadAllText(_sessionLogPath);
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var remaining = new List<string>();
                foreach (var raw in lines)
                {
                    try
                    {
                        var o = JObject.Parse(raw);
                        var qid = (string)o["queueId"];
                        if (!string.IsNullOrWhiteSpace(qid) && sentOk.Contains(qid))
                            continue;
                        remaining.Add(raw);
                    }
                    catch
                    {
                        remaining.Add(raw);
                    }
                }

                var newContent = remaining.Count > 0
                    ? string.Join(Environment.NewLine, remaining) + Environment.NewLine
                    : string.Empty;
                WriteAllTextAtomic(_sessionLogPath, newContent);
            }
        }

        private static string BuildBehaviorWatchEnvelope(string sessionLineJson)
        {
            var data = JToken.Parse(sessionLineJson);
            var saveEvent = new SaveEvent
            {
                eventType = "behavior_watch",
                clientName = MachineName.Instance.Name,
                customerId = StoreCfgJson.Instance.customerID,
                data = data,
                timestampUtc = DateTime.UtcNow,
            };
            return JsonConvert.SerializeObject(saveEvent);
        }

        private static async Task<bool> TryPostEventLogAsync(string json)
        {
            try
            {
                using var client = new HttpClient();
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(StoreCfgJson.Instance.serverUrl + "/session-log/create", content).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    DebugLog.WriteLine("[BehaviorWatch] SendSessionQueue OK: " + responseString);
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                DebugLog.WriteLine($"[BehaviorWatch] SendSessionQueue error: {response.StatusCode}, {error}");
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[BehaviorWatch] SendSessionQueue exception: {ex.Message}");
                return false;
            }
        }

        private static void WriteAllTextAtomic(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content);

            try
            {
                File.Replace(tmp, path, null);
            }
            catch
            {
                File.Move(tmp, path, true);
            }
        }

        private static string BuildConditionSummary(List<Condition> conditionGroup)
        {
            if (conditionGroup == null || conditionGroup.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" and ", conditionGroup.Select(ToHumanCondition));
        }

        private static string ToHumanCondition(Condition c)
        {
            string eventType = (c.eventType ?? string.Empty).ToLowerInvariant();
            string field = (c.field ?? string.Empty).ToLowerInvariant();
            string op = (c.@operator ?? "eq").ToLowerInvariant();
            string valueText = c.value?.ToString() ?? string.Empty;

            if (op == "eq")
            {
                if (eventType == "vpn" && field == "vpn")
                    return IsTruthy(c.value) ? "VPN is ON" : "VPN is OFF";
                if (eventType == "internet" && field == "internet")
                    return IsTruthy(c.value) ? "Internet is ON" : "Internet is OFF";
                if (eventType == "clipboard" && field == "type")
                    return $"Clipboard contains {NormalizeTextValue(valueText)}";
                if (eventType == "focus_window" && field == "appname")
                    return $"Active window is {NormalizeTextValue(valueText)}";
                if (eventType == "ai_interaction" && field == "ison")
                    return IsTruthy(c.value) ? "AI interaction is active" : "AI interaction is inactive";
                if (eventType == "ai_interaction" && field == "type")
                    return $"AI interaction type is {NormalizeTextValue(valueText)}";
                if (eventType == "ai_interaction" && field == "keyword")
                    return $"AI provider is {NormalizeTextValue(valueText)}";
                if (field == "applist")
                    return $"Detected app includes {NormalizeTextValue(valueText)}";
                if (field == eventType)
                    return IsTruthy(c.value) ? $"{ToTitle(eventType)} is ON" : $"{ToTitle(eventType)} is OFF";
            }

            return $"{ToTitle(eventType)} {c.field} {ToReadableOperator(op)} {NormalizeTextValue(valueText)}";
        }

        private static string NormalizeTextValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Equals("True", StringComparison.OrdinalIgnoreCase) ? "ON"
                : value.Equals("False", StringComparison.OrdinalIgnoreCase) ? "OFF"
                : value;
        }

        private static bool IsTruthy(object? value)
        {
            if (value is bool b) return b;
            if (value is string s)
            {
                if (bool.TryParse(s, out bool parsed)) return parsed;
                if (int.TryParse(s, out int n)) return n != 0;
            }
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            return false;
        }

        private static string ToReadableOperator(string op) => op switch
        {
            "eq" => "is",
            "gte" => "is greater than or equal to",
            "lte" => "is less than or equal to",
            "gt" => "is greater than",
            "lt" => "is less than",
            _ => op
        };

        private static string ToTitle(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            return string.Join(" ", input.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
        }

        private void UpdateOverview(
            string ruleId,
            string ruleDescription,
            int startedDelta,
            int extendedDelta,
            int finishedDelta,
            int resolvedDelta,
            int deferredDelta,
            int ignoredDelta)
        {
            try
            {
                WatchOverview overview;
                if (File.Exists(_overviewPath))
                {
                    var json = File.ReadAllText(_overviewPath);
                    overview = JsonConvert.DeserializeObject<WatchOverview>(json) ?? new WatchOverview();
                }
                else
                {
                    overview = new WatchOverview();
                }

                overview.generatedAtUtc = DateTime.UtcNow;
                ApplyDelta(overview.totals, startedDelta, extendedDelta, finishedDelta, resolvedDelta, deferredDelta, ignoredDelta);

                if (!overview.byRule.TryGetValue(ruleId, out var ruleOverview))
                {
                    ruleOverview = new RuleOverview();
                    overview.byRule[ruleId] = ruleOverview;
                }
                ruleOverview.ruleId = ruleId;
                ruleOverview.ruleDescription = ruleDescription ?? string.Empty;
                ApplyDelta(ruleOverview, startedDelta, extendedDelta, finishedDelta, resolvedDelta, deferredDelta, ignoredDelta);

                File.WriteAllText(_overviewPath, JsonConvert.SerializeObject(overview, Formatting.Indented));
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[BehaviorWatch] Failed to update overview: {ex.Message}");
            }
        }

        private static void ApplyDelta(RuleOverview target, int started, int extended, int finished, int resolved, int deferred, int ignored)
        {
            target.started += started;
            target.extended += extended;
            target.finished += finished;
            target.resolved += resolved;
            target.deferred += deferred;
            target.ignored += ignored;
        }
    }
}

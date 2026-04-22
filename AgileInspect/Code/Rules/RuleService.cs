using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgileInspect.Code.Ipc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgileInspect.Code.Rules
{
    public class RuleService
    {
        public static Dictionary<string, JToken> _latestStates = new();
        private static readonly Dictionary<string, DateTime> _lastActionTimeByRule = new();
        private static string? _lastRuleWithSticky;
        private const string ConfigFileName = "event_last_state_log.json";
        private static string GetRoamingConfigPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
            Directory.CreateDirectory(folder); // ensure folder exists
            return Path.Combine(folder, ConfigFileName);
        }

        public static void Load()
        {
            // Persistence for event_last_state_log.json is disabled.
            // Original code kept below for quick restore:
            /*
            try
            {
                string lastStatePath = GetRoamingConfigPath();

                if (!File.Exists(lastStatePath))
                {
                    File.Create(lastStatePath).Dispose();
                    DebugLog.WriteLine($"Created new last states file at {lastStatePath}");
                    return;
                }

                string json = File.ReadAllText(lastStatePath);
                _latestStates = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json)
                            ?? new Dictionary<string, JToken>();

                DebugLog.WriteLine($"Loaded last states from {lastStatePath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load last states: {ex.Message}");
            }
            */
            _latestStates ??= new Dictionary<string, JToken>();
        }

        public static void Save(string key, JToken value)
        {
            JToken oldValue; 
            _latestStates.TryGetValue(key,out oldValue);
            if (JToken.DeepEquals(oldValue, value))
                return;
            _latestStates[key] = value;
#if DEBUG
            try
            {
                var stateJson = JsonConvert.SerializeObject(_latestStates);
                DebugLog.WriteLine($"[RuleService] _latestStates updated by '{key}': {stateJson}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[RuleService] Failed to serialize _latestStates for debug log: {ex.Message}");
            }
#endif

            // Persistence for event_last_state_log.json is disabled.
            // Original code kept below for quick restore:
            /*
            try
            {
                string lastStatePath = GetRoamingConfigPath();
                string json = JsonConvert.SerializeObject(_latestStates);
                File.WriteAllText(lastStatePath, json);

                DebugLog.WriteLine($"Saved last states to {lastStatePath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to save last states: {ex.Message}");
            }
            */
        }

        public static void CheckRules(String pluginName)
        {
            var ruleSettings = StoreCfgJson.Instance.rules;
            if (ruleSettings == null || ruleSettings.Count == 0) return;
            var indexedRules = ruleSettings
                .Select((rule, index) => new { rule, index })
                .Where(o =>
                {
                    var condition = o.rule.conditions.Where(i => i.Where(e => e.eventType == pluginName).Any()).ToList();
                    return condition.Any();
                })
                .ToList();
            foreach (var indexedRule in indexedRules)
            {
                string ruleId = $"rule_{indexedRule.index}";
                bool anyGroupMatched = false;

                foreach (var conditionGroup in indexedRule.rule.conditions)
                {
                    bool groupMatched = true;

                    foreach (var condition in conditionGroup)
                    {
                        if (!SatisfiedCondition(condition))
                        {
                            groupMatched = false;
                            break;
                        }
                    }

                    if (groupMatched)
                    {
                        anyGroupMatched = true;
                        if (IsRuleCooldownActive(ruleId, indexedRule.rule))
                        {
                            break;
                        }
                        _lastActionTimeByRule[ruleId] = DateTime.UtcNow;
                        ExecuteActions(indexedRule.rule.actions, conditionGroup, ruleId);
                        break;
                    }
                }

                if (!anyGroupMatched)
                {
                    ClearCooldown(ruleId);
                    if (_lastRuleWithSticky == ruleId)
                    {
                        _lastRuleWithSticky = null;
                        var hideAction = new { action = Constant.ACTION_AGILEMARK_HIDE_MESSAGE, @params = new Dictionary<string, object>() };
                        IpcService.Instance.SendRequest(JsonConvert.SerializeObject(hideAction));
                        PluginContext.Log("RuleEngine", $"[STICKY] Hide message for {ruleId} (rule no longer matched)");
                    }
                }
            }
        }

        private static bool IsRuleCooldownActive(string ruleId, AgileInspect.Rules rule)
        {
            if (!_lastActionTimeByRule.TryGetValue(ruleId, out DateTime lastFired))
                return false;

            int cooldownSec = rule.cooldownSec ?? StoreCfgJson.Instance.defaultRuleCooldownSec;
            if (cooldownSec <= 0)
                return false;

            double elapsed = (DateTime.UtcNow - lastFired).TotalSeconds;
            if (elapsed < cooldownSec)
            {
                PluginContext.Log("RuleEngine", $"[COOLDOWN] {ruleId} skipped, {cooldownSec - (int)elapsed}s remaining");
                return true;
            }

            return false;
        }

        public static void ClearCooldown(string ruleId)
        {
            _lastActionTimeByRule.Remove(ruleId);
        }

        public static bool IsRuleCurrentlyMatched(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return false;
            if (!ruleId.StartsWith("rule_", StringComparison.OrdinalIgnoreCase)) return false;

            var suffix = ruleId.Substring("rule_".Length);
            if (!int.TryParse(suffix, out int index)) return false;

            var ruleSettings = StoreCfgJson.Instance.rules;
            if (ruleSettings == null || index < 0 || index >= ruleSettings.Count) return false;

            var rule = ruleSettings[index];
            foreach (var conditionGroup in rule.conditions)
            {
                bool groupMatched = true;
                foreach (var condition in conditionGroup)
                {
                    if (!SatisfiedCondition(condition))
                    {
                        groupMatched = false;
                        break;
                    }
                }

                if (groupMatched)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SatisfiedCondition(Condition condition)
        {
            // filter condition
            if (condition.eventFilters.Count > 0)
            {
                foreach (var item in condition.eventFilters.Keys)
                {
                    object? obj = UnwrapJsonElement(_latestStates.TryGetValue(item, out var o) ? o : null);
                    object? con = condition.eventFilters[item];
                    if (con == null || obj == null) { 
                        return false;
                    }
                    if (con.GetType() != obj.GetType()) return false;
                    if (!Equals(con,obj)) {  return false; }
                }
            }
            
            object? expected = UnwrapJsonElement(condition.value);
            if (expected == null) return true;

            object? value = UnwrapJsonElement(_latestStates.TryGetValue(condition.eventType, out var v) ? v : null);
            if (value == null) return false;
            if (condition.eventType.Equals("antivirus"))
            {
                var data = JsonConvert.DeserializeObject<JObject>(value.ToString());
                var appList = (JArray)data["appList"];
                var combine = true;
                foreach (var item in appList)
                {
                    if (condition.eventFilters.Count > 0)
                    {
                        foreach (var key in condition.eventFilters.Keys)
                        {
                            var left = JToken.FromObject(condition.eventFilters[key]);
                             var right = item[key];
                            var compare = JToken.Equals(right, left);
                            combine = combine || compare;
                        }
                    } else
                    {
                        var left = JToken.FromObject(condition.value);
                        var right = item[condition.field];
                        var compare = JToken.Equals(right, left);
                        combine = combine || compare;
                    }
                }
                value = combine;
            }
            else if (condition.eventFilters.Count == 0)
            {
                value = (((JToken)value)[condition.field]).ToObject<object>();
                if (value == null) return false;
            }

            if (value is JArray jsonArray)
            {
                var expectedToken = JToken.FromObject(expected);
                if (expected is System.Collections.IEnumerable expectedEnumerable && expected is not string)
                {
                    foreach (var item in expectedEnumerable)
                    {
                        var itemToken = JToken.FromObject(item);
                        if (!jsonArray.Any(x => JToken.DeepEquals(x, itemToken)))
                        {
                            return false;
                        }
                    }
                    return true;
                }

                return jsonArray.Any(x => JToken.DeepEquals(x, expectedToken));
            }

            if (value is System.Collections.IEnumerable valueEnumerable && value is not string)
            {
                var actualItems = valueEnumerable.Cast<object?>().ToList();
                if (expected is System.Collections.IEnumerable expectedEnumerable && expected is not string)
                {
                    var expectedItems = expectedEnumerable.Cast<object?>().ToList();
                    return expectedItems.All(exp => actualItems.Any(act => Equals(act, exp)));
                }

                return actualItems.Any(act => Equals(act, expected));
            }

            if (expected.GetType() != value.GetType()) return false;

            string op = condition.@operator?.ToLower();

            if (value is IComparable cmpActual && expected is IComparable cmpExpected)
            {
                int cmp = cmpActual.CompareTo(cmpExpected);

                return op switch
                {
                    "eq" => cmp == 0,
                    "gte" => cmp >= 0,
                    "lte" => cmp <= 0,
                    "gt" => cmp > 0,
                    "lt" => cmp < 0,
                    _ => false
                };
            }

            return op == "eq" && Equals(value, expected);
        }

        private static object? UnwrapJsonElement(object? obj)
        {
            if (obj is not JsonElement je) return obj;

            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt32(out int i) ? i :
                                       je.TryGetInt64(out long l) ? l :
                                       je.TryGetDouble(out double d) ? d : null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => je.EnumerateArray().Select(x => UnwrapJsonElement(x)).ToList(),
                _ => je.ToString()
            };
        }

        private static void ExecuteActions(List<Action> actions, List<Condition> conditionGroup, string ruleId)
        {
            string conditionStr = string.Join(", ", conditionGroup.Select(c => $"{c.eventType} {c.@operator} {c.value}"));
            var ruleMatchEvent = new JObject
            {
                ["ruleId"] = ruleId,
                ["conditions"] = JArray.FromObject(conditionGroup),
                ["actions"] = JArray.FromObject(actions)
            };
            EventQueueService.Instance.EnqueueByEventType("rule_match", ruleMatchEvent);
            BehaviorWatchService.Instance.OnRuleMatched(ruleId, conditionGroup);
            List<Action> messageAction = new List<Action>();
            foreach (var action in actions)
            {

                switch (action.action)
                {
                    case Constant.ACTION_SEND_TO_SERVER:
                        PluginContext.Log("RuleEngine", $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;
                    case Constant.ACTION_AGILEMARK_UPDATE_SETTING:
                        IpcService.Instance.SendRequest(JsonConvert.SerializeObject(action));
                        break;
                    case Constant.ACTION_AGILEMARK_SHOW_MESSAGE:
                        messageAction.Add(action);
                        break;
                    case Constant.ACTION_AGILEMARK_SHOW_TOAST:
                        IpcService.Instance.SendRequest(JsonConvert.SerializeObject(action));
                        break;

                    default:
                        PluginContext.Log("RuleEngine", $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        break;
                }
                PluginContext.Log("RuleEngine", $"Executing action: {action.action}");
            }
            if (messageAction.Count > 0)
            {
                var stateProperties = FlattenDeep(_latestStates);
                messageAction = messageAction.Where(o =>
                {
                    return (o.@params.TryGetValue("params", out object @params) && @params is JToken messageParams && messageParams["message"] != null);
                }).Select(o =>
                {
                    o.@params.TryGetValue("params", out object @params);
                    var json = @params as JObject;
                    var message = json["message"].ToObject<String>();
                    if (message == null)
                    {
                        return null;
                    }
                    message = ReplacePlaceholders(message, stateProperties);
                    json["message"] = message;
                    o.@params["params"] = json;
                    return o;
                })
                .Where(o=>o!=null)
                .ToList() ;
                IpcService.Instance.SendRequest(JsonConvert.SerializeObject(messageAction));
                _lastRuleWithSticky = ruleId;
            }
        }
        public static string ReplacePlaceholders(string template, JObject data)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            // Find all placeholders: {xxx}
            var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\{([a-zA-Z0-9_]+)\}");

            foreach (Match match in matches)
            {
                string placeholder = match.Value;     // e.g. "{name}"
                string key = match.Groups[1].Value;    // e.g. "name"

                // Check if JObject contains the key
                if (data.TryGetValue(key, out JToken value) && value != null)
                {
                    template = template.Replace(placeholder, value.ToString());
                }
                else
                {
                    // Remove placeholder entirely if key missing
                    template = template.Replace(placeholder, "");
                }
            }

            return template.Trim();
        }

        public static JObject FlattenDeep(Dictionary<string, JToken> input)
        {
            JObject result = new JObject();

            void FlattenToken(JToken token)
            {
                if (token is JObject obj)
                {
                    FlattenObject(obj);
                }
                else if (token is JArray arr)
                {
                    foreach (var item in arr)
                        FlattenToken(item);
                }
            }

            void FlattenObject(JObject obj)
            {
                // 1. CHILDREN FIRST
                foreach (var prop in obj.Properties())
                {
                    FlattenToken(prop.Value);
                }

                // 2. THEN PARENT LEVEL (NO OVERRIDE)
                foreach (var prop in obj.Properties())
                {
                    if (!result.TryGetValue(prop.Name, out _)) // do not override
                    {
                        result[prop.Name] = prop.Value.DeepClone();
                    }
                }
            }

            // START: flatten input Dictionary
            foreach (var kv in input)
            {
                FlattenToken(kv.Value);
            }

            // Add top-level keys last (no override)
            foreach (var kv in input)
            {
                if (!result.TryGetValue(kv.Key, out _))
                    result[kv.Key] = kv.Value.DeepClone();
            }

            return result;
        }

    }
}

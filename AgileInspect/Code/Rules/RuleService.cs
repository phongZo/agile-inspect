using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace AgileInspect.Code.Rules
{
    public class RuleService
    {
        public static Dictionary<string, object> _latestStates = new();
        private const string ConfigFileName = "event_last_state_log.json";

        public static void Load()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string lastStatePath = Path.Combine(basePath, ConfigFileName);

                if (!File.Exists(lastStatePath))
                {
                    DebugLog.WriteLine($"Last states file not found: {lastStatePath}");
                    StoreCfgJson.Instance = new StoreCfgJson(); // fallback default
                    return;
                }

                string json = File.ReadAllText(lastStatePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _latestStates = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options)
                            ?? new Dictionary<string, object>();

                DebugLog.WriteLine($"Loaded last states from {lastStatePath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load last states: {ex.Message}");
                DebugLog.WriteLine($"Fallback defaut last states success");

                StoreCfgJson.Instance = new StoreCfgJson(); // fallback
            }
        }

        public static void Save(string key, object value)
        {
            if (_latestStates.TryGetValue(key, out var oldValue) && Equals(oldValue, value))
                return;
            _latestStates[key] = value;

            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string lastStatePath = Path.Combine(basePath, ConfigFileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(_latestStates, options);
                File.WriteAllText(lastStatePath, json);

                DebugLog.WriteLine($"Saved last states to {lastStatePath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to save last states: {ex.Message}");
            }
        }

        public static void CheckRules()
        {
            var ruleSettings = StoreCfgJson.Instance.ruleConfig?.ruleSettings;
            if (ruleSettings == null || ruleSettings.Count == 0) return;

            foreach (var rule in ruleSettings)
            {
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
                        ExecuteActions(rule.actions, conditionGroup);
                        break;
                    }
                }
            }
        }

        private static bool SatisfiedCondition(Condition condition)
        {
            object? expected = UnwrapJsonElement(condition.value);
            if (expected == null) return true;

            object? value = UnwrapJsonElement(_latestStates.TryGetValue(condition.field, out var v) ? v : null);
            if (value == null) return false;

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
                _ => je.ToString()
            };
        }

        private static void ExecuteActions(List<Action> actions, List<Condition> conditionGroup)
        {
            string conditionStr = string.Join(", ", conditionGroup.Select(c => $"{c.field} {c.@operator} {c.value}"));
            foreach (var action in actions)
            {

                switch (action.action)
                {
                    case Constant.ACTION_SEND_TO_SERVER:
                        PluginContext.Log("RuleEngine", $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;
                    case Constant.ACTION_AGILEMARK_UPDATE_SETTING:
                        break;

                    default:
                        PluginContext.Log("RuleEngine", $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        break;
                }
                PluginContext.Log("RuleEngine", $"Executing action: {action.action}");
            }
        }
    }
}

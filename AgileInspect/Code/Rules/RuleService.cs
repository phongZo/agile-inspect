using AgileInspect.Code.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgileInspect.Code.Rules
{
    public class RuleService
    {
        public static void CheckRules(object value, string pluginName)
        {
            var ruleSettings = StoreCfgJson.Instance.ruleConfig?.ruleSettings;
            if (ruleSettings == null || ruleSettings.Count == 0) return;

            string fieldName = StoreCfgLoader.mapPluginNameToEventType(pluginName);

            foreach (var rule in ruleSettings)
            {
                foreach (var conditionGroup in rule.conditions)
                {
                    bool groupMatched = true;

                    foreach (var condition in conditionGroup)
                    {
                        if (!EvaluateCondition(condition, value, fieldName))
                        {
                            groupMatched = false;
                            break;
                        }
                    }

                    if (groupMatched)
                    {
                        string conditionStr = string.Join(", ", conditionGroup.Select(c => $"{c.field} {c.@operator} {c.value}"));
                        ExecuteActions(rule.actions, pluginName, conditionGroup);
                        return;
                    }
                }
            }
        }

        private static bool EvaluateCondition(Condition condition, object value, string fieldName)
        {
            if (!string.Equals(condition.field, fieldName, StringComparison.OrdinalIgnoreCase))
                return false;

            object expected = condition.value;

            if (expected == null || value == null) return false;

            if (expected is System.Text.Json.JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        expected = je.GetString();
                        break;
                    case System.Text.Json.JsonValueKind.Number:
                        if (value is int)
                            expected = je.GetInt32();
                        else if (value is long)
                            expected = je.GetInt64();
                        else if (value is float)
                            expected = je.GetSingle();
                        else if (value is double)
                            expected = je.GetDouble();
                        else
                            expected = je.GetDouble();
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        expected = je.GetBoolean();
                        break;
                    default:
                        expected = je.ToString();
                        break;
                }
            }

            string op = condition.@operator?.ToLower();


            if (value is IComparable cmpActual && expected is IComparable cmpExpected)
            {
                int cmp = cmpActual.CompareTo(cmpExpected);

                return op switch
                {
                    "eq" => cmp == 0,
                    "gte" => cmp >= 0,
                    "lte" => cmp <= 0,
                    _ => false
                };
            }

            return op == "eq" && Equals(value, expected);
        }

        private static void ExecuteActions(List<Action> actions, string pluginName, List<Condition> conditionGroup)
        {
            string conditionStr = string.Join(", ", conditionGroup.Select(c => $"{c.field} {c.@operator} {c.value}"));
            foreach (var action in actions)
            {

                switch (action.action)
                {
                    case "SendToServer":
                        PluginContext.Log(pluginName, $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;

                    default:
                        PluginContext.Log(pluginName, $"[RULE MATCH] Action: '{action.action}' | Conditions: {conditionStr}");
                        break;
                }
                PluginContext.Log("RuleEngine", $"Executing action: {action.action}");
            }
        }
    }
}

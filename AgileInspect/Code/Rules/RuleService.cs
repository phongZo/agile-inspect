using AgileInspect.Code.Settings;
using System;
using System.Collections.Generic;

namespace AgileInspect.Code.Rules
{
    public class RuleService
    {
        public static void CheckRules(object value, string pluginName)
        {
            var ruleSettings = StoreCfgJson.Instance.ruleConfig?.ruleSettings;
            if (ruleSettings == null || ruleSettings.Count == 0) return;

            string fieldName = StoreCfgLoader.Instance.MapPluginNameToEventType(pluginName);

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
                        ExecuteActions(rule.actions, pluginName);
                        return;
                    }
                }
            }
        }

        private static bool EvaluateCondition(Condition condition, object value, string fieldName)
        {
            if (!string.Equals(condition.field, fieldName, StringComparison.OrdinalIgnoreCase))
                return false;

            var expected = condition.value;
            string op = condition.@operator?.ToLower();

            if (expected == null || value == null) return false;
            if (value.GetType() != expected.GetType()) return false;

            try
            {
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
            catch
            {
                return false;
            }
        }

        private static void ExecuteActions(List<Action> actions, string pluginName)
        {
            foreach (var action in actions)
            {

                switch (action.action)
                {
                    case "SendToServer":
                        PluginContext.Log(pluginName, $"[RULE MATCH] Action: '{action.action}' | Conditions: ");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;

                    default:
                        PluginContext.Log(pluginName, $"[RULE MATCH] Action: '{action.action}' | Conditions: ");
                        break;
                }
                PluginContext.Log("RuleEngine", $"Executing action: {action.action}");
            }
        }
    }
}

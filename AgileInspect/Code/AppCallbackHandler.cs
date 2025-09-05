using AgileInspect.Code;
using AgileInspect.Code.PluginContracts;
using System.Text.Json;

namespace AgileInspect
{
    public class AppCallbackHandler : IAppCallback
    {
        #region Singleton
        public static readonly AppCallbackHandler Instance = new AppCallbackHandler();

        private AppCallbackHandler() { }
        #endregion

        private readonly Dictionary<string, string> _latestFields = new();

        public void OnLog(string pluginName, string message)
        {
            DebugLog.WriteLine($"[{pluginName}] {message}");
        }

        public void OnDetectionResult(string pluginName, string jsonResult)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResult);

                if (dict == null) return;
                EventQueueService.Instance.Enqueue(pluginName, dict);
                bool updated = true;

                foreach (var kvp in dict)
                {
                    string key = kvp.Key.ToLowerInvariant();
                    string value = kvp.Value.ToString().ToLowerInvariant();

                    if (!_latestFields.ContainsKey(key) || _latestFields[key] != value)
                    {
                        _latestFields[key] = value;
                    }
                }

                if (updated)
                {
                    //OnLog(pluginName, $"[CHECK RULE] Start.");
                    //CheckRules(pluginName);
                }
            }
            catch (Exception ex)
            {
                OnLog(pluginName, $"OnDetectionResult error: {ex}");
            }
        }

        private void CheckRules(string pluginName)
        {
            var ruleSettings = StoreCfgJson.Instance.ruleConfig?.ruleSettings;
            if (ruleSettings == null || ruleSettings.Count == 0) return;

            foreach (var rule in ruleSettings)
            {
                if (rule.plugins == null || !rule.plugins.Any(p => p.Equals(pluginName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                List<string> failedConditions = new();
                bool isMatched = true;

                foreach (var cond in rule.conditions)
                {
                    string key = cond.field.ToLowerInvariant();
                    string expected = cond.expected.ToLowerInvariant();

                    if (!_latestFields.TryGetValue(key, out var actualValue) || actualValue != expected)
                    {
                        failedConditions.Add($"{cond.field}: expected '{expected}', actual '{actualValue ?? "null"}'");
                        isMatched = false;
                    }
                }

                if (!isMatched)
                {
                    string failedStr = string.Join("; ", failedConditions);
                    OnLog(pluginName, $"[RULE NOT MATCHED] Plugins: [{string.Join(", ", rule.plugins)}] | Failed: {failedStr}");
                    continue;
                }

                string conditionStr = string.Join(", ", rule.conditions.Select(c => $"{c.field}={c.expected}"));
                switch (rule.action)
                {
                    case "SendToServer":
                        OnLog(pluginName, $"[RULE MATCH] Action: '{rule.action}' | Conditions: {conditionStr}");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;

                    default:
                        OnLog(pluginName, $"[RULE] Unknown action '{rule.action}' | Conditions: {conditionStr}");
                        break;
                }
            }
        }
    }

}

using AgileInspect.Code.PluginContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AgileInspect
{
    public class AppCallbackHandler : IAppCallback
    {
        #region Singleton
        public static AppCallbackHandler Instance { get; set; }
        public AppCallbackHandler()
        {
            Instance = this;
        }
        #endregion
        private RuleConditionQueueService RuleConditionQueueService { get; set; } = new RuleConditionQueueService();
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
                logEvent(pluginName, dict);
                //bool updated = false;
                bool updated = true;

                foreach (var kvp in dict)
                {
                    string key = kvp.Key.ToLowerInvariant();
                    string value = kvp.Value.ToString().ToLowerInvariant();

                    if (!_latestFields.ContainsKey(key) || _latestFields[key] != value)
                    {
                        _latestFields[key] = value;
                        //updated = true;
                    }
                }

                if (updated)
                {
                    OnLog(pluginName, $"[CHECK RULE] Start.");
                    CheckRules(pluginName);
                }
            }
            catch (Exception ex)
            {
                OnLog(pluginName, $"OnDetectionResult error: {ex}");
            }
        }
        private string mapPluginNameToEventType(string pluginName)
        {
            if (pluginName == "WatermarkDetectorPlugin")
            {
                return "watermark";
            }
            else if (pluginName == "ServiceDetectorPlugin")
            {
                return "service_changed";
            }
            else if (pluginName == "FileChangePlugin")
            {
                return "file_changed";
            }
            else
                return "";
        }
        private async void logEvent(string pluginName, Dictionary<string, JsonElement> json)
        {
            using var client = new HttpClient();

            // Create the SaveEvent object
            var saveEvent = new SaveEvent
            {
                eventType = mapPluginNameToEventType(pluginName),
                clientName = MachineName.Instance.Name,
                customerId = StoreCfgJson.Instance.CustomerID,
                data = json
            };
            // Serialize the object to JSON
            var jsonContent = JsonSerializer.Serialize(saveEvent);
            EventLog.WriteLine(jsonContent);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send POST request
            var response = await client.PostAsync(StoreCfgJson.Instance.ServerUrl + "/event-log/create", content);

            // Optionally read the response
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                DebugLog.WriteLine("[LogEvent] Success: " + responseString);
            }
            else
            {
                DebugLog.WriteLine($"[LogEvent] Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }
        }
        private void CheckRules(string pluginName)
        {
            var ruleSettings = StoreCfgJson.Instance.RuleConfig?.RuleSettings;
            if (ruleSettings == null || ruleSettings.Count == 0) return;

            foreach (var rule in ruleSettings)
            {
                if (rule.Plugins == null || !rule.Plugins.Any(p => p.Equals(pluginName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                List<string> failedConditions = new();
                bool isMatched = true;

                foreach (var cond in rule.Conditions)
                {
                    string key = cond.Field.ToLowerInvariant();
                    string expected = cond.Expected.ToLowerInvariant();

                    if (!_latestFields.TryGetValue(key, out var actualValue) || actualValue != expected)
                    {
                        failedConditions.Add($"{cond.Field}: expected '{expected}', actual '{actualValue ?? "null"}'");
                        isMatched = false;
                    }
                }

                if (!isMatched)
                {
                    string failedStr = string.Join("; ", failedConditions);
                    OnLog(pluginName, $"[RULE NOT MATCHED] Plugins: [{string.Join(", ", rule.Plugins)}] | Failed: {failedStr}");
                    continue;
                }

                string conditionStr = string.Join(", ", rule.Conditions.Select(c => $"{c.Field}={c.Expected}"));
                switch (rule.Action)
                {
                    case "SendToServer":
                        OnLog(pluginName, $"[RULE MATCH] Action: '{rule.Action}' | Conditions: {conditionStr}");
                        //RuleConditionQueueService.Instance.EnqueueMatchedConditions(rule.Plugins,rule.Conditions);
                        break;

                    default:
                        OnLog(pluginName, $"[RULE] Unknown action '{rule.Action}' | Conditions: {conditionStr}");
                        break;
                }
            }
        }
    }

}

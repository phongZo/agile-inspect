using AgileInspect.Code;
using AgileInspect.Code.PluginContracts;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private EventQueueService EventQueueService { get; set; } = new EventQueueService();

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
                    OnLog(pluginName, $"[CHECK RULE] Start.");
                    //CheckRules(pluginName);
                }
            }
            catch (Exception ex)
            {
                OnLog(pluginName, $"OnDetectionResult error: {ex}");
            }
        }
    }

}

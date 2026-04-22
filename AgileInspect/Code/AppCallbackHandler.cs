using AgileInspect.Code;
using AgileInspect.Code.PluginContracts;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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

        private readonly Dictionary<string, string> _latestEventPayloadByType = new();

        public void OnLog(string pluginName, string message)
        {
            DebugLog.WriteLine($"[{pluginName}] {message}");
        }

        public void OnDetectionResult(string pluginName, JToken jsonResult)
        {
            try
            {
                var dict = jsonResult.ToObject<JObject>();

                if (dict == null) return;
                var eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName);
                if (string.IsNullOrWhiteSpace(eventType)) return;
                var payload = JsonConvert.SerializeObject(dict);
                bool updated = !_latestEventPayloadByType.TryGetValue(eventType, out string? oldPayload)
                    || !string.Equals(oldPayload, payload, StringComparison.Ordinal);

                if (updated)
                {
                    _latestEventPayloadByType[eventType] = payload;
                    EventQueueService.Instance.Enqueue(pluginName, dict);
                }

                RuleService.CheckRules(eventType);
            }
            catch (Exception ex)
            {
                OnLog(pluginName, $"OnDetectionResult error: {ex}");
            }
        }
    }

}

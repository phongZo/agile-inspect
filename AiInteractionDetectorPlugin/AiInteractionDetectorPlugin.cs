using AgileInspect.Code.PluginContracts;
using AiInteractionDetectorPlugin.Code.Settings;
using System;
using System.Text.Json;

namespace AiInteractionDetectorPlugin
{
    public class AiInteractionDetectorPlugin : IAiInteractionDetectorPlugin
    {
        public string Name => "AiInteractionDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            // Deserialize Event Params (Keywords)
            if (!string.IsNullOrEmpty(eventParamsJson))
            {
                try
                {
                    // PluginContext.Log(Name, $"[DEBUG] Received Params: {eventParamsJson}"); 
                    var eventParams = JsonSerializer.Deserialize<EventParams>(eventParamsJson);
                    if (eventParams != null) 
                    {
                        setting.eventParams = eventParams;
                        PluginContext.Log(Name, $"[DEBUG] Deserialized Domains: {setting.eventParams.aiDomains?.Length ?? 0}");
                    }
                }
                catch (Exception ex)
                {
                    PluginContext.Log(Name, $"Failed to parse eventParams: {ex.Message}");
                }
            }

            // Deserialize Trigger Params (Interval)
            if (!string.IsNullOrEmpty(triggerParamsJson))
            {
                try
                {
                    var triggerParams = JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson);
                    if (triggerParams != null) setting.triggerParams = triggerParams;
                }
                catch (Exception ex)
                {
                    PluginContext.Log(Name, $"Failed to parse triggerParams: {ex.Message}");
                }
            }

            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            StoreCfgJson.Instance.eventSetting = setting;
            PluginContext.Log(Name, "Parameters is set");
        }

        public void Start()
        {
            PluginContext.Log(Name, "Start");
            AiInteractionDetector.Instance.StartWatcher();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stop");
            AiInteractionDetector.Instance.StopWatcher();
        }
    }
}

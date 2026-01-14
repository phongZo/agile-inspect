using AgileInspect.Code.PluginContracts;
using PasteDetectorPlugin.Code;
using PasteDetectorPlugin.Code.Settings;
using System;
using System.Text.Json;

namespace PasteDetectorPlugin
{
    public class PasteDetectorPlugin : IAppPlugin
    {
        public string Name => "PasteDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        private PasteDetector _detector;

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
            _detector = new PasteDetector(StoreCfgJson);
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
                : null;

            setting.eventParams = parsedEventParams ?? setting.eventParams;
            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            setting.triggerParams = parsedTriggerParams ?? setting.triggerParams;

            StoreCfgJson.Instance.eventSetting = setting;

            PluginContext.Log(Name, "Parameters is set");
        }

        public void Start()
        {
            PluginContext.Log(Name, "Start");
            _detector?.Start();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped");
            _detector?.Stop();
        }
    }
}

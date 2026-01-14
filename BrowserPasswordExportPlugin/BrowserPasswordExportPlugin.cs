using AgileInspect.Code.PluginContracts;
using BrowserPasswordExportPlugin.Code;
using BrowserPasswordExportPlugin.Code.Settings;
using System.Text.Json;

namespace BrowserPasswordExportPlugin
{
    public class BrowserPasswordExportPlugin : IAppPlugin
    {
        public string Name => "BrowserPasswordExportPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        private BrowserExportWatcher _watcher;

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
            _watcher = new BrowserExportWatcher(StoreCfgJson);
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

            PluginContext.Log(Name, "Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");
            
            _watcher?.Start();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _watcher?.Stop();
        }
    }
}

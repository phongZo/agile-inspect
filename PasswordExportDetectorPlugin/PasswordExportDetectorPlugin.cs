using AgileInspect.Code.PluginContracts;
using PasswordExportDetectorPlugin.Code;
using PasswordExportDetectorPlugin.Code.Settings;
using System;
using System.Text.Json;

namespace PasswordExportDetectorPlugin
{
    public class PasswordExportDetectorPlugin : IAppPlugin
    {
        public string Name => "PasswordExportDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        private PasswordExportWatcher _watcher;

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
            _watcher = new PasswordExportWatcher(StoreCfgJson);
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
            _watcher?.Start();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped");
            _watcher?.Stop();
        }
    }
}

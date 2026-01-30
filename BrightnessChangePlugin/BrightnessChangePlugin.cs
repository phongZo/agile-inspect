using AgileInspect.Code.PluginContracts;
using BrightnessChangePlugin.Code;
using System.Text.Json;

namespace BrightnessChangePlugin
{
    public class BrightnessChangePlugin : IBrightnessChangePlugin
    {
        private AsyncTimerService _brightnessTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public BrightnessChangeWatcher BrightnessChangeWatcher { get; set; } = new BrightnessChangeWatcher();
        public string pluginName => "BrightnessChangePlugin";

        public void Initialize()
        {
            PluginContext.Log(pluginName, $"Initialize");
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

            PluginContext.Log(pluginName, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(pluginName, "Start");
            PluginContext.Log(pluginName, $"triggerType: {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _brightnessTimerService = new AsyncTimerService(interval * 1000, CheckBrightnessTimerCallbackAsync);
                _brightnessTimerService.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                BrightnessChangeWatcher.Instance.StartWatcher();
            }
            else
            {
                PluginContext.Log(pluginName, $"[Brightness] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _brightnessTimerService?.Stop();
            _brightnessTimerService?.Dispose();
            _brightnessTimerService = null;
            BrightnessChangeWatcher.Instance.StopWatcher();
        }

        private async Task CheckBrightnessTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[CheckBrightness] interval hit");
            await Task.Run(() => BrightnessChangeWatcher.Instance.GetCurrentBrightness());
        }
    }
}

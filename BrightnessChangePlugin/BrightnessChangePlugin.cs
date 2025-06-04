using AgileInspect.Code.PluginContracts;
using BrightnessChangePlugin.Code;
using System.Text.Json;

namespace BrightnessChangePlugin
{
    public class BrightnessChangePlugin : IBrightnessChangePlugin
    {
        private AsyncTimerService _brightnessTimerService;
        public Permission Permission { get; set; } = new Permission();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public BrightnessChangeWatcher BrightnessChangeWatcher { get; set; } = new BrightnessChangeWatcher();
        public string Name => "BrightnessChangePlugin";
        
        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
                : null;

            setting.EventParams = parsedEventParams ?? setting.EventParams;
            setting.TriggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.TriggerType;
            setting.TriggerParams = parsedTriggerParams ?? setting.TriggerParams;

            StoreCfgJson.Instance.EventSetting = setting;

            PluginContext.Log(Name, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var interval = setting.TriggerParams.Interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"TriggerType: {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
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
                DebugLog.WriteLine($"[Brightness] Unsupported TriggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _brightnessTimerService?.Stop();
            _brightnessTimerService?.Dispose();
            _brightnessTimerService = null;
            BrightnessChangeWatcher.Instance.StopWatcher();
        }

        private async Task CheckBrightnessTimerCallbackAsync()
        {
            PluginContext.Log(Name, "[CheckBrightness] Interval hit");
            await Task.Run(() => BrightnessChangeWatcher.Instance.GetCurrentBrightness());
        }

    }
}

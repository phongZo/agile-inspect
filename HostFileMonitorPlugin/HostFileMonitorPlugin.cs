using AgileInspect.Code.PluginContracts;

namespace HostFileMonitorPlugin
{
    public class HostFileMonitorPlugin : IHostFileMonitorPlugin
    {
        private AsyncTimerService _hostFileMonitorTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public HostFileMonitor HostFileMonitor { get; set; } = new HostFileMonitor();
        public string pluginName => "HostFileMonitorPlugin";

        public void Initialize()
        {
            PluginContext.Log(pluginName, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
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
                _hostFileMonitorTimerService = new AsyncTimerService(interval * 1000, CheckHostFileTimerCallbackAsync);
                _hostFileMonitorTimerService.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                HostFileMonitor.Instance.StartWatcher();
            }
            else
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _hostFileMonitorTimerService?.Stop();
            _hostFileMonitorTimerService?.Dispose();
            _hostFileMonitorTimerService = null;
            HostFileMonitor.Instance.StopWatcher();
        }

        private async Task CheckHostFileTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[HostFileMonitor] interval hit");
            try
            {
                await Task.Run(() => HostFileMonitor.Instance.Checksum());
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection failed: {ex}");
            }
        }
    }
}

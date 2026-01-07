using AgileInspect.Code.PluginContracts;

namespace FileMonitorPlugin
{
    public class FileMonitorPlugin : IFileMonitorPlugin
    {
        private AsyncTimerService _fileMonitorTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public FileMonitor FileMonitor { get; set; } = new FileMonitor();
        public string Name => "FileMonitorPlugin";

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
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

            PluginContext.Log(Name, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _fileMonitorTimerService = new AsyncTimerService(interval * 1000, CheckExternalDiskTimerCallbackAsync);
                _fileMonitorTimerService.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                FileMonitor.Instance.StartWatcher();
            }
            else
            {
                PluginContext.Log(Name, $"[FileMonitor] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _fileMonitorTimerService?.Stop();
            _fileMonitorTimerService?.Dispose();
            _fileMonitorTimerService = null;
            FileMonitor.Instance.StopWatcher();
        }

        private async Task CheckExternalDiskTimerCallbackAsync()
        {
            PluginContext.Log(Name, "[FileMonitor] interval hit");
            try
            {
                await Task.Run(() => FileMonitor.Instance.Checksum());
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Detection failed: {ex}");
            }
        }
    }
}

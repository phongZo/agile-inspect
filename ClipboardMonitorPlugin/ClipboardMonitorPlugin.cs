using AgileInspect.Code.PluginContracts;
using ClipboardMonitorPlugin.Code;

namespace ClipboardMonitorPlugin
{
    public class ClipboardMonitorPlugin : IClipboardMonitorPlugin
    {
        private AsyncTimerService _clipboardTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public ClipboardMonitor ClipboardMonitor { get; set; } = new ClipboardMonitor();
        public string pluginName => "ClipboardMonitorPlugin";

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

            // Start a single hidden PowerShell instance up front for Purview queries.
            PurviewGetFileStatus.Initialize();

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _clipboardTimerService = new AsyncTimerService(interval * 1000, CheckClipboardTimerCallbackAsync);
                _clipboardTimerService.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                ClipboardMonitor.Instance.StartWatcher();
            }
            else
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _clipboardTimerService?.Stop();
            _clipboardTimerService?.Dispose();
            _clipboardTimerService = null;
            ClipboardMonitor.Instance.StopWatcher();
            PurviewGetFileStatus.Shutdown();
        }

        private async Task CheckClipboardTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[ClipboardMonitor] interval hit");
            try
            {
                await ClipboardMonitor.Instance.ProcessClipboardFiles();
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection failed: {ex}");
            }
        }
    }
}

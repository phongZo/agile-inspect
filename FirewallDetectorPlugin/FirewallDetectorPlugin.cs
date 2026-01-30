using AgileInspect.Code.PluginContracts;

namespace FirewallDetectorPlugin
{
    public class FirewallDetectorPlugin : IFirewallDetectorPlugin
    {
        private AsyncTimerService _firewallTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public FirewallDetector FirewallDetector { get; set; } = new FirewallDetector();
        public string pluginName => "FirewallDetectorPlugin";

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
                _firewallTimerService = new AsyncTimerService(interval * 1000, CheckFirewallTimerCallbackAsync);
                _firewallTimerService.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                FirewallDetector.Instance.StartWatcher();
            }
            else
            {
                PluginContext.Log(pluginName, $"[FirewallDetector] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _firewallTimerService?.Stop();
            _firewallTimerService?.Dispose();
            _firewallTimerService = null;
            FirewallDetector.Instance.StopWatcher();
        }

        private async Task CheckFirewallTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[FirewallDetector] interval hit");
            try
            {
                await Task.Run(() => FirewallDetector.Instance.CheckFirewall());
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection failed: {ex}");
            }
        }
    }
}

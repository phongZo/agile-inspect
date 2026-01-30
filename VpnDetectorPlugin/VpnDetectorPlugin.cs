using AgileInspect.Code.PluginContracts;

namespace VpnDetectorPlugin
{
    public class VpnDetectorPlugin : IVpnDetectorPlugin
    {
        private AsyncTimerService _vpnTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public VpnDetector VpnDetector { get; set; } = new VpnDetector();
        public string pluginName => "VpnDetectorPlugin";

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
                _vpnTimerService = new AsyncTimerService(interval * 1000, CheckVpnTimerCallbackAsync);
                _vpnTimerService.Start();
            }
            else
            {
                PluginContext.Log(pluginName, $"[VpnDetector] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _vpnTimerService?.Stop();
            _vpnTimerService?.Dispose();
            _vpnTimerService = null;
        }

        private async Task CheckVpnTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[VpnDetector] interval hit");
            try
            {
                await Task.Run(() => VpnDetector.Instance.CheckVpn());
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection failed: {ex}");
            }
        }
    }
}

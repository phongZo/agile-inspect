using AgileInspect.Code.PluginContracts;

namespace InternetDetectorPlugin
{
    public class InternetDetectorPlugin : IInternetDetectorPlugin
    {
        private AsyncTimerService _internetTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public InternetDetector InternetDetector { get; set; } = new InternetDetector();
        public string Name => "InternetDetectorPlugin";

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
                _internetTimerService = new AsyncTimerService(interval * 1000, CheckInternetTimerCallbackAsync);
                _internetTimerService.Start();
            }
            else
            {
                PluginContext.Log(Name, $"[InternetDetector] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _internetTimerService?.Stop();
            _internetTimerService?.Dispose();
            _internetTimerService = null;
        }

        private async Task CheckInternetTimerCallbackAsync()
        {
            PluginContext.Log(Name, "[InternetDetector] interval hit");
            try
            {
                await Task.Run(() => InternetDetector.Instance.CheckAllowInternet());
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Detection failed: {ex}");
            }
        }
    }
}

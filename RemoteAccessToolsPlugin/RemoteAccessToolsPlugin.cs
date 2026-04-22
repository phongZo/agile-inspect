using AgileInspect.Code.PluginContracts;

namespace RemoteAccessToolsPlugin
{
    public class RemoteAccessToolsPlugin : IRemoteAccessToolsPlugin
    {
        private AsyncTimerService _timerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public RemoteAccessToolsDetector RemoteAccessToolsDetector { get; set; } = new RemoteAccessToolsDetector();
        public string pluginName => "RemoteAccessToolsPlugin";

        public void Initialize()
        {
            PluginContext.Log(pluginName, "Initialize");
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

            PluginContext.Log(pluginName, "Parameters are set");
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
                _timerService = new AsyncTimerService(interval * 1000, CheckTimerCallbackAsync);
                _timerService.Start();
            }
            else
            {
                PluginContext.Log(pluginName, $"[RemoteAccessTools] Unsupported triggerType '{triggerType}', plugin will not start.");
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _timerService?.Stop();
            _timerService?.Dispose();
            _timerService = null;
        }

        private async Task CheckTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[RemoteAccessTools] interval hit");
            try
            {
                await Task.Run(() => RemoteAccessToolsDetector.Instance.Check());
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection failed: {ex}");
            }
        }
    }
}

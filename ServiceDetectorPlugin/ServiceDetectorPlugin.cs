using AgileInspect.Code;
using AgileInspect.Code.PluginContracts;
using ServiceDetectorPlugin.Code;
using System.Text.Json;

namespace ServiceDetectorPlugin
{
    public class ServiceDetectorPlugin : IServiceDetectorPlugin
    {
        private AsyncTimerService _serviceDetectorTimer;
        public void SetCallback(IAppCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            PluginContext.SetCallback(callback);
        }
        public ServiceDetector ServiceDetector { get; set; } = new ServiceDetector();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public string Name => "ServiceDetectorPlugin";

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }


        public void Start()
        {

            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var interval = setting.TriggerParams.Interval;

            PluginContext.Log(Name,$"{Name} started.");
            PluginContext.Log(Name,$"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run Interval
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"{Name}: Realtime trigger not implemented, fallback to interval.");
            }
            else
            {
                PluginContext.Log(Name, $"{Name}: Unsupport trigger type '{triggerType}, plugin will not start.");
                return;
            }

            _serviceDetectorTimer = new AsyncTimerService(interval * 1000, CheckServiceTimersAsync);
            _serviceDetectorTimer.Start();
        }


        public void Stop()
        {
            PluginContext.Log(Name, $"Stopped");
            _serviceDetectorTimer?.Stop();
            _serviceDetectorTimer?.Dispose();
            _serviceDetectorTimer = null;
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

        private async Task CheckServiceTimersAsync()
        {
            PluginContext.Log(Name, "[CheckService] Interval hit");
            await Task.Run(() => ServiceDetector.Instance.CheckServices());
        }

    }
}

using AgileInspect.Code.PluginContracts;
using ServiceMonitorPlugin.Code;
using System.Text.Json;

namespace ServiceMonitorPlugin
{
    public class ServiceMonitorPlugin : IServiceMonitorPlugin
    {
        private AsyncTimerService _serviceDetectorTimer;
        public ServiceMonitor ServiceDetector { get; set; } = new ServiceMonitor();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public string Name => "ServiceMonitoringPlugin";

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }


        public void Start()
        {

            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(Name, $"{Name} started.");
            PluginContext.Log(Name, $"{Name}: triggerType : {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run interval
            }
            else if (triggerType.Equals("realtime", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"{Name}: realtime trigger not implemented, fallback to interval.");
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

            PluginContext.Log(Name, $"Parameters is set ");
        }

        private async Task CheckServiceTimersAsync()
        {
            PluginContext.Log(Name, "[CheckService] interval hit");
            await Task.Run(() => ServiceMonitor.Instance.CheckServices());
        }

    }
}

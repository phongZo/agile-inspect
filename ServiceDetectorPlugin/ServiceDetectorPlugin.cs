using AgileInspect.Code.PluginContracts;
using ServiceDetectorPlugin.Code;
using System.Text.Json;

namespace ServiceDetectorPlugin
{
    public class ServiceDetectorPlugin : IServiceDetectorPlugin
    {
        private Timer? LogTimer;
        private Timer? ServiceDetectorTimer;

        public Permission Permission { get; set; } = new Permission();
        public ServiceDetector ServiceDetector { get; set; } = new ServiceDetector();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public string Name => "ServiceDetectorPlugin";

        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write("", false);
            DebugLog.Write($"--------{Name} Initialize-------");
        }

        public void Start()
        {
            // LogRotation Callback
            LogTimer = new Timer(LogRotateTimerCallBack, null, 0, 24 * 60 * 60 * 1000); // 86400000 ms

            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var interval = setting.TriggerParams.Interval;

            DebugLog.WriteLine($"{Name} started.");
            DebugLog.WriteLine($"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run Interval
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.WriteLine($"{Name}: Realtime trigger not implemented, fallback to interval.");
            }
            else
            {
                DebugLog.WriteLine($"{Name}: Unsupport trigger type '{triggerType}, plugin will not start.");
                return;
            }

            ServiceDetectorTimer = new Timer(CheckServiceTimersCallBack, null, 0, interval * 1000);

        }


        public void Stop()
        {
            DebugLog.WriteLine($"{Name} stopped.");
            ServiceDetectorTimer?.Dispose();
            ServiceDetectorTimer = null;
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
        }

        private void CheckServiceTimersCallBack(object? state)
        {
            DebugLog.WriteLine("[CheckService] Interval hit");
            ServiceDetector.Instance.CheckServices();
        }

        private void LogRotateTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            LogRotate.HandleRotation();
        }
    }
}

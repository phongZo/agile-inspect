using AgileInspect.Code.PluginContracts;
using NetworkChangePlugin.Code.Settings.Web;
using System.Text.Json;

namespace NetworkChangePlugin
{
    public class NetworkChangePlugin : INetworkChangePlugin
    {
        public NetworkAddressChangeWatcher NetworkAddressChangeWatcher { get; set; } = new NetworkAddressChangeWatcher();
        public Permission Permission { get; set; } = new Permission();
        private string TriggerType = "Interval";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public string Name => "NetworkChangePlugin";
        private Timer LogRotateTimer;
       

        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write("", false);
            DebugLog.Write($"--------{Name} Initialize-------");
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

        public void Start()
        {
            LogRotateTimer = new Timer(LogRotateTimerCallBack, null, 0, 24 * 60 * 60 * 1000); // 86400000 ms

            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;

            DebugLog.WriteLine($"{Name} started.");
            DebugLog.WriteLine($"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("RealTime", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run interval
            }
            else if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.WriteLine($"{Name}: TriggerType 'Interval' is not implemented, fallback to RealTime.");
            }
            else
            {
                DebugLog.WriteLine($"{Name}: Unknown TriggerType '{triggerType}, plugin will not start.");
                return;
            }
            NetworkAddressChangeWatcher.Instance.StartWatcher();

        }

        public void Stop()
        {
            DebugLog.WriteLine($"{Name} stopped.");
            NetworkAddressChangeWatcher.Instance.StopWatcher();
        }

        private void LogRotateTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            LogRotate.HandleRotation();
        }
    }
}

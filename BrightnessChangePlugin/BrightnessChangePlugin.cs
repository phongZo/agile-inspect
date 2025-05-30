using AgileInspect.Code.PluginContracts;
using BrightnessChangePlugin.Code;
using System.Text.Json;

namespace BrightnessChangePlugin
{
    public class BrightnessChangePlugin : IBrightnessChangePlugin
    {
        private Timer? LogRotateTimer;
        private Timer? BrightnessChangeTimer;

        public string Name => "BrightnessChangePlugin";

        public Permission Permission { get; set; } = new Permission();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public BrightnessChangeWatcher BrightnessChangeWatcher { get; set; } = new BrightnessChangeWatcher();

        public void Initialize()
        {
            DebugLog.Write("", false);
            DebugLog.WriteLine($"{Name} initialized.");
            DebugLog.Write("---------------------------------", false);
            DebugLog.Init();
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
            var triggerType = setting.TriggerType ?? "Realtime";
            var interval = setting.TriggerParams?.Interval > 0 ? setting.TriggerParams.Interval : 10; // default 10s

            DebugLog.WriteLine($"{Name} started.");
            DebugLog.WriteLine($"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                BrightnessChangeTimer = new Timer(CheckBrightnessTimerCallBack, null, 0, interval * 1000);
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                BrightnessChangeWatcher.Instance.StartWatcher();
            }
            else
            {
                DebugLog.WriteLine($"[Brightness] Unsupported TriggerType '{triggerType}, plugin will not start.");
                return;
            }
        }


        public void Stop()
        {
            DebugLog.WriteLine($"{Name} stopped.");
            BrightnessChangeTimer?.Dispose();
            BrightnessChangeTimer = null;
            BrightnessChangeWatcher.Instance.StopWatcher();
        }

        private void LogRotateTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            LogRotate.HandleRotation();
        }
        private void CheckBrightnessTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[CheckBrightness] Interval hit");
            BrightnessChangeWatcher.Instance.GetCurrentBrightness();
        }

    }
}

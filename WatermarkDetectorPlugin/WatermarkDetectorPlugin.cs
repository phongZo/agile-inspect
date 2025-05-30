using AgileInspect.Code.PluginContracts;
using Compunet.YoloSharp;
using System.Reflection;
using System.Text.Json;

namespace WatermarkDetectorPlugin
{
    public class WatermarkDetectorPlugin : IWatermarkDetectorPlugin
    {
        private YoloPredictor? Predictor;
        private Timer WatermarkDetectorTimer;
        private Timer LogRotationTimer;

        public Permission Permission { get; set; } = new Permission();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public WatermarkDetector WatermarkDetector { get; set; } = new WatermarkDetector();
        public string Name => "WatermarkDetectorPlugin";

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
            LogRotationTimer = new Timer(LogRotateTimerCallBack, null, 0, 24 * 60 * 60 * 1000); // 86400000 ms

            DebugLog.WriteLine($"[{Name}] Start");

            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var interval = setting.TriggerParams.Interval;

            DebugLog.WriteLine($"[{Name}] TriggerType: {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run interval
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.WriteLine($"[{Name}] Realtime not implemented, fallback to Interval.");
            }
            else
            {
                DebugLog.WriteLine($"[{Name}] Unsupport TriggerType '{triggerType}', plugin will not start.");
                return;
            }

            WatermarkDetectorTimer = new Timer(async _ =>
            {
                await RunDetectionAsync();
            }, null, 0, interval * 1000);
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        private async Task RunDetectionAsync()
        {
            try
            {
                string resourceName = "WatermarkDetectorPlugin.Model.best.onnx";
                using var modelStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (modelStream == null)
                {
                    DebugLog.WriteLine($"[{Name}] Model resource '{resourceName}' not found.");
                    return;
                }

                var detector = new WatermarkDetector(modelStream);
                await detector.ProcessAsync();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[{Name}] Detection failed: {ex}");
            }
        }

        private void LogRotateTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            LogRotate.HandleRotation();
        }
    }
}

using AgileInspect.Code.PluginContracts;
using System.Reflection;
using System.Text.Json;

namespace WatermarkDetectorPlugin
{
    public class WatermarkDetectorPlugin : IWatermarkDetectorPlugin
    {
        private AsyncTimerService _watermarkDetectorTimer;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public WatermarkDetector WatermarkDetector { get; set; } = new WatermarkDetector();
        public string Name => "WatermarkDetectorPlugin";

        public void SetCallback(IAppCallback callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            PluginContext.SetCallback(callback);
        }

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
            string resourceName = "WatermarkDetectorPlugin.Model.best.onnx";
            using var modelStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (modelStream == null)
            {
                PluginContext.Log(Name, $"Model resource '{resourceName}' not found.");
                return;
            }

            WatermarkDetector = new WatermarkDetector(modelStream);
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();

            if (!string.IsNullOrWhiteSpace(eventParamsJson))
            {
                var parsedEventParams = JsonSerializer.Deserialize<EventParams>(eventParamsJson);
                if (parsedEventParams != null) setting.EventParams = parsedEventParams;
            }

            if (!string.IsNullOrWhiteSpace(triggerType))
            {
                setting.TriggerType = triggerType;
            }

            if (!string.IsNullOrWhiteSpace(triggerParamsJson))
            {
                var parsedTriggerParams = JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson);
                if (parsedTriggerParams != null) setting.TriggerParams = parsedTriggerParams;
            }

            StoreCfgJson.Instance.EventSetting = setting;

            PluginContext.Log(Name, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var intervalSeconds = setting.TriggerParams.Interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"TriggerType: {triggerType}");

            if (!triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"Unsupported TriggerType '{triggerType}', fallback to Interval");
            }

            _watermarkDetectorTimer = new AsyncTimerService(intervalSeconds * 1000, WatermarkDetectorCallback);

            _watermarkDetectorTimer.Start();
        }

        public void Stop()
        {
            _watermarkDetectorTimer?.Stop();
            _watermarkDetectorTimer?.Dispose();

            PluginContext.Log(Name, "Stopped");
        }
        private async Task WatermarkDetectorCallback()
        {
            PluginContext.Log(Name, "Interval hit");

            try
            {
                await WatermarkDetector.ProcessAsync();
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Detection failed: {ex}");
            }
        }

    }
}

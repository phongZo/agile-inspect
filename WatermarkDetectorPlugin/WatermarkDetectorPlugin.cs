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

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            if (!string.IsNullOrWhiteSpace(eventParamsJson))
            {
                var parsedEventParams = JsonSerializer.Deserialize<EventParams>(eventParamsJson);
                if (parsedEventParams != null) setting.eventParams = parsedEventParams;
            }

            if (!string.IsNullOrWhiteSpace(triggerType))
            {
                setting.triggerType = triggerType;
            }

            if (!string.IsNullOrWhiteSpace(triggerParamsJson))
            {
                var parsedTriggerParams = JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson);
                if (parsedTriggerParams != null) setting.triggerParams = parsedTriggerParams;
            }

            StoreCfgJson.Instance.eventSetting = setting;

            PluginContext.Log(Name, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var intervalSeconds = setting.triggerParams.interval;

            string resourceName = "WatermarkDetectorPlugin.Model.best.onnx";
            using var modelStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (modelStream == null)
            {
                PluginContext.Log(Name, $"Model resource '{resourceName}' not found.");
                return;
            }

            WatermarkDetector = new WatermarkDetector(modelStream);

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            if (!triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"Unsupported triggerType '{triggerType}', fallback to interval");
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
            PluginContext.Log(Name, "interval hit");

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

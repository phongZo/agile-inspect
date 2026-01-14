using AgileInspect.Code.PluginContracts;
using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using PrintScreenDetectorPlugin.Code.Settings;

namespace PrintScreenDetectorPlugin
{
    public class PrintScreenDetectorPlugin : IPrintScreenDetectorPlugin
    {
        public string Name => "PrintScreenDetectorPlugin";
        private AsyncTimerService _timerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_SNAPSHOT = 0x2C;

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            if (!string.IsNullOrEmpty(triggerParamsJson))
            {
                try
                {
                    var triggerParams = JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson);
                    if (triggerParams != null) setting.triggerParams = triggerParams;
                }
                catch { }
            }
            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            StoreCfgJson.Instance.eventSetting = setting;
            PluginContext.Log(Name, "Parameters is set");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            double intervalMs = 100; // Default for realtime polling
            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                intervalMs = interval * 1000;
            }
            else if (!triggerType.Equals("realtime", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"[PrintScreen] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }

            _timerService = new AsyncTimerService(intervalMs, CheckPrintScreenAsync);
            _timerService.Start();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _timerService?.Stop();
            _timerService?.Dispose();
            _timerService = null;
        }

        private async Task CheckPrintScreenAsync()
        {
            try
            {
                short state = GetAsyncKeyState(VK_SNAPSHOT);
                if ((state & 0x8000) != 0)
                {
                    PluginContext.Log(Name, "PrintScreen pressed");
                    
                    var result = new {
                        eventType = "printscreen_detect",
                        timestamp = DateTime.Now
                    };
                    //PluginContext.SendDetectionResult(Name, System.Text.Json.JsonSerializer.Serialize(result));

                    await Task.Delay(50); // Prevent multiple detections
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Error: {ex.Message}");
            }
        }
    }
}

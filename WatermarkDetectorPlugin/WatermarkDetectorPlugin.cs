using AgileInspect.Code.PluginContracts;
using AgileInspect.Code.Ipc;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace WatermarkDetectorPlugin
{
    public class WatermarkDetectorPlugin : IWatermarkDetectorPlugin
    {
        private AsyncTimerService _watermarkTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public string Name => "WatermarkDetectorPlugin";

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
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

            PluginContext.Log(Name, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _watermarkTimerService = new AsyncTimerService(interval * 1000, CheckWatermarkTimerCallbackAsync);
                _watermarkTimerService.Start();
            }
            else
            {
                PluginContext.Log(Name, $"[WatermarkDetector] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _watermarkTimerService?.Stop();
            _watermarkTimerService?.Dispose();
            _watermarkTimerService = null;
        }

        private async Task CheckWatermarkTimerCallbackAsync()
        {
            PluginContext.Log(Name, "[WatermarkDetector] interval hit");
            try
            {
                await Task.Run(async () =>
                {
                    // Send IPC message to AgileMark with 3s timeout
                    string response = await IpcService.Instance.SendRequestWithResponseAsync("{\"action\":\"check_watermark_status\"}", 3000);
                    
                    bool visible = false;
                    if (!string.IsNullOrEmpty(response))
                    {
                        try
                        {
                            var responseObj = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(response);
                            if (responseObj != null && responseObj["visible"] != null)
                            {
                                visible = responseObj["visible"].Value<bool>();
                            }
                        }
                        catch (Exception ex)
                        {
                            PluginContext.Log(Name, $"Failed to parse response: {ex.Message}");
                        }
                    }
                    // If no reply in 3s (response is null/empty), watermark is off

                    var output = new JObject
                    {
                        ["visible"] = visible
                    };
                    
                    PluginContext.Log(Name, $"[WatermarkDetector] Watermark status: {(visible)}");
                    RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(Name), output);
                    PluginContext.SendDetectionResult(Name, output);
                });
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Detection failed: {ex}");
            }
        }
    }
}


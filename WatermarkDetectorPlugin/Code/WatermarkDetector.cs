using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using AgileInspect.Code.MonitorManagement;
using Compunet.YoloSharp;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;

namespace WatermarkDetectorPlugin
{
    public class WatermarkDetector
    {
        #region Singleton
        public static WatermarkDetector Instance { get; set; }
        public WatermarkDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly YoloPredictor Predictor;
        string pluginName = "WatermarkDetectorPlugin";
        byte[] imageData = null;

        public WatermarkDetector(Stream modelStream)
        {
            // Copy stream into a temporary file to be passed to predictor
            string tempPath = Path.GetTempFileName();
            using (var file = File.OpenWrite(tempPath))
            {
                modelStream.CopyTo(file);
            }
            var config = new YoloConfiguration
            {
                Confidence = 0.8f,
            };

            var options = new YoloPredictorOptions
            {
                Configuration = config
            };
            Predictor = new YoloPredictor(tempPath, options);
        }

        public async Task ProcessAsync()
        {
            bool isOn = true;
            var visibleArray = new List<Dictionary<string, object>>();

            var screens = MultipleMonitors.GetMonitors();
            foreach (var screen in screens)
            {
                using (Bitmap bmp = GetBitmapFromScreen(screen.bounds))
                {
                    bool result = await DetectWatermarkAsync(bmp, screen.deviceName);
                    if (!result)
                    {
                        isOn = false;
                    }
                    visibleArray.Add(new Dictionary<string, object>
                    {
                        { "monitor", screen.deviceName },
                        { "value", result }
                    });
                }
            }

            string value = isOn ? "ON" : "OFF";
            PluginContext.Log(pluginName, $"[WatermarkDetector] Watermark detected: {(isOn ? "on" : "off")}");

            // save last state and check rule
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), value);

            var resultObj = new Dictionary<string, object>
            {
                { "visible", visibleArray },
            };
            string jsonResult = JsonConvert.SerializeObject(resultObj);
            PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

        private async Task<bool> DetectWatermarkAsync(Bitmap bitmap, string deviceName)
        {
            try
            {
                imageData = ConvertBitmapToBytes(bitmap);
                var result = await Predictor.DetectAsync(imageData);
                bool isOn = result.Count > 0;
                PluginContext.Log(pluginName, $"[WatermarkDetector] DeviceName {deviceName}: {(isOn ? "on" : "off")}");
                return isOn;
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection {deviceName} error: {ex}");
                return false;
            }
        }

        private Bitmap GetBitmapFromScreen(Rectangle bounds)
        {
            var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            }

            return bmp;
        }

        public static byte[] ConvertBitmapToBytes(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}

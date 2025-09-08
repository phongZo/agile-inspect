using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Compunet.YoloSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

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
            try
            {
                Bitmap bitmap;
                try
                {
                    bitmap = CapturePrimaryScreen();
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"[WatermarkDetector] Watermark detected: off");
                    var resultObj = new Dictionary<string, object>
                    {
                        { "visible", false },
                    };
                    string jsonResult = JsonSerializer.Serialize(resultObj);
                    PluginContext.SendDetectionResult(pluginName, jsonResult);
                    return;
                }

                using (bitmap)
                {
                    imageData = ConvertBitmapToBytes(bitmap);
                    var result = await Predictor.DetectAsync(imageData);

                    bool isOn = result.Count > 0;
                    PluginContext.Log(pluginName, $"[WatermarkDetector] Watermark detected: {(isOn ? "on" : "off")}");

                    string value = isOn ? "ON" : "OFF";
                    string eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName);
                    RuleService.Save(eventType, value);
                    RuleService.CheckRules();

                    var resultObj = new Dictionary<string, object>
                    {
                        { "visible", isOn ? true : false },
                    };
                    string jsonResult = JsonSerializer.Serialize(resultObj);
                    PluginContext.SendDetectionResult(pluginName, jsonResult);
                }

            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection error: {ex}");
            }
        }

        public static Bitmap CapturePrimaryScreen()
        {
            var screenWidth = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SystemMetric.SM_CYSCREEN);

            var bmp = new Bitmap(screenWidth, screenHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
            }

            return bmp;
        }
        public static byte[] ConvertBitmapToBytes(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric smIndex);

        private enum SystemMetric
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1,
        }
    }
}

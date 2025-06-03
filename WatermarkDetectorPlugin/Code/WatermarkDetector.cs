using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
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
                Confidence = 0.9f,
                IoU = 0.45f,
                KeepAspectRatio = true,
                ApplyAutoOrient = true
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
                string dllDir = Path.Combine(AppContext.BaseDirectory, pluginName);
                string screenshotDir = Path.Combine(dllDir, "screenshot");
                Directory.CreateDirectory(screenshotDir);

                string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string tempImagePath = Path.Combine(screenshotDir, fileName);

                CapturePrimaryScreen(tempImagePath);
                PluginContext.Log(pluginName, $"Captured screenshot: {tempImagePath}");

                string outputFolder = Path.Combine(screenshotDir, "result");
                Directory.CreateDirectory(outputFolder);

                string outputPath = Path.Combine(outputFolder, fileName);

                //await Predictor.PredictAndSaveAsync(tempImagePath, outputPath);
                YoloResult<Detection> result = await Predictor.DetectAsync(tempImagePath);

                bool isOn = result.Count > 0;

                PluginContext.Log(pluginName, $"[WatermarkDetector] Done: {fileName} | Watermark detected: {(isOn ? "on" : "off")}");

                var resultObj = new Dictionary<string, object>
                {
                    { "watermark", isOn ? "on" : "off" },
                };
                string jsonResult = JsonSerializer.Serialize(resultObj);
                PluginContext.SendDetectionResult(pluginName, jsonResult);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Detection error: {ex}");
            }
        }

        public static void CapturePrimaryScreen(string savePath)
        {
            var screenWidth = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SystemMetric.SM_CYSCREEN);

            using var bmp = new Bitmap(screenWidth, screenHeight);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
            bmp.Save(savePath, ImageFormat.Png);
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

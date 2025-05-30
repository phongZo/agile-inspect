using Compunet.YoloSharp;
using Compunet.YoloSharp.Plotting;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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

        public WatermarkDetector(Stream modelStream)
        {
            // Copy stream into a temporary file to be passed to predictor
            string tempPath = Path.GetTempFileName();
            using (var file = File.OpenWrite(tempPath))
            {
                modelStream.CopyTo(file);
            }

            Predictor = new YoloPredictor(tempPath);
        }

        public async Task ProcessAsync()
        {
            try
            {
                string screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshot");
                Directory.CreateDirectory(screenshotDir);

                string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string tempImagePath = Path.Combine(screenshotDir, fileName);

                CapturePrimaryScreen(tempImagePath);
                DebugLog.WriteLine($"[WatermarkDetector] Captured screenshot: {tempImagePath}");

                string outputFolder = Path.Combine(screenshotDir, "result");
                Directory.CreateDirectory(outputFolder);

                string outputPath = Path.Combine(outputFolder, fileName);

                await Predictor.PredictAndSaveAsync(tempImagePath, outputPath);
                var result = await Predictor.DetectAsync(tempImagePath);

                DebugLog.WriteLine($"[WatermarkDetector] Done: {fileName} | Result: {result}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[WatermarkDetector] ERROR: {ex}");
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

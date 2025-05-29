using AgileInspect.Code.PluginContracts;
using Compunet.YoloSharp;
using Compunet.YoloSharp.Plotting;
using System.Reflection;

namespace WatermarkDetectorPlugin
{
    public class WatermarkDetectorPlugin : IWatermarkDetectorPlugin
    {
        private YoloPredictor? Predictor;
        public Permission Permission { get; set; } = new Permission();
        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write("Initializing WatermarkDetectorPlugin...", false);
        }
        public void Start()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartAsync();
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"[Start] Unhandled exception: {ex.Message}");
                }
            });
        }

        public async Task StartAsync()
        {
            try
            {
                DebugLog.WriteLine("[StartAsync] Begin");

                string resourceName = "WatermarkDetectorPlugin.Model.best.onnx";
                string tempFilePath = Path.GetTempFileName();
                DebugLog.WriteLine($"[StartAsync] Temp ONNX path: {tempFilePath}");

                DebugLog.WriteLine("[StartAsync] Extracting embedded model...");
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (resource == null)
                    {
                        DebugLog.WriteLine($"[StartAsync] ERROR: Resource '{resourceName}' not found.");
                        return;
                    }

                    using (var file = File.OpenWrite(tempFilePath))
                    {
                        await resource.CopyToAsync(file);
                        DebugLog.WriteLine("[StartAsync] Model extracted successfully.");
                    }
                }

                DebugLog.WriteLine("[StartAsync] Initializing YoloPredictor...");
                using var predictor = new YoloPredictor(tempFilePath);
                DebugLog.WriteLine("[StartAsync] Predictor initialized.");

                string inputFolder = @"D:\YOLO\test";
                string outputFolder = @"D:\YOLO\result";

                Directory.CreateDirectory(outputFolder);
                DebugLog.WriteLine($"[StartAsync] Input folder: {inputFolder}");
                DebugLog.WriteLine($"[StartAsync] Output folder: {outputFolder}");

                string[] imageFiles = Directory.GetFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly);
                DebugLog.WriteLine($"[StartAsync] Found {imageFiles.Length} files in input folder.");

                foreach (string imagePath in imageFiles)
                {
                    string extension = Path.GetExtension(imagePath).ToLower();
                    if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                    {
                        DebugLog.WriteLine($"[StartAsync] Skipping unsupported file: {imagePath}");
                        continue;
                    }

                    string fileName = Path.GetFileName(imagePath);
                    string outputPath = Path.Combine(outputFolder, fileName);

                    DebugLog.WriteLine($"[StartAsync] Predicting: {imagePath}");

                    await predictor.PredictAndSaveAsync(imagePath, outputPath);
                    var result = await predictor.DetectAsync(imagePath);

                    DebugLog.WriteLine($"[StartAsync] Prediction done: {fileName}, Result: {result}");
                }

                DebugLog.WriteLine("[StartAsync] Processing completed.");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[StartAsync] ERROR: {ex.Message}");
                DebugLog.WriteLine($"[StartAsync] STACKTRACE: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    DebugLog.WriteLine($"[StartAsync] INNER ERROR: {ex.InnerException.Message}");
                    DebugLog.WriteLine($"[StartAsync] INNER STACKTRACE: {ex.InnerException.StackTrace}");
                }
            }
        }

        public void Stop()
        {
            Predictor?.Dispose();
        }
    }
}

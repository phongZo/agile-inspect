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
            string eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName);
            bool isOn = true;

            var screens = GetMonitors();
            foreach (var screen in screens)
            {
                using (Bitmap bmp = GetBitmapFromScreen(screen.bounds))
                {
                    bool result = await DetectWatermarkAsync(bmp, screen.deviceName);
                    if (!result)
                    {
                        isOn = false;
                    }
                }
            }

            string value = isOn ? "ON" : "OFF";
            PluginContext.Log(pluginName, $"[WatermarkDetector] Watermark detected: {(isOn ? "on" : "off")}");

            // save last state and check rule
            RuleService.Save(eventType, value);
            RuleService.CheckRules();

            var resultObj = new Dictionary<string, object>
            {
                { "visible", isOn ? true : false },
            };
            string jsonResult = JsonSerializer.Serialize(resultObj);
            PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

        private List<(Rectangle bounds, string deviceName)> GetMonitors()
        {
            var screens = new List<(Rectangle, string)>();

            MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    int width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                    int height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                    Rectangle bounds = new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, width, height);
                    screens.Add((bounds, mi.szDevice));
                }

                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return screens;
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

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
    }
}

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;

namespace ResolutionDetectorPlugin.Code
{
    public class ResolutionDetector
    {
        #region Singleton
        public static ResolutionDetector Instance { get; set; }
        public ResolutionDetector()
        {
            Instance = this;
        }
        #endregion

        private HwndSource? _source;
        string pluginName => "ResolutionDetectorPlugin";

        public void StartWatcher()
        {
            try
            {
                HwndSourceParameters param = new HwndSourceParameters("ResolutionDetectorMessageWindow");
                param.WindowStyle = 0x800000; // WS_OVERLAPPED
                param.UsesPerPixelOpacity = false;

                _source = new HwndSource(param);
                _source.AddHook(WndProc);

                PluginContext.Log(pluginName, "[ResolutionDetectorPlugin] Realtime watcher started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ResolutionDetectorPlugin] Failed to start realtime watcher: {ex.Message}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                    _source.Dispose();
                    _source = null;
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ResolutionDetectorPlugin] Error stopping realtime watcher: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const uint WM_DISPLAYCHANGE = 0x007e;

            switch ((uint)msg)
            {
                case WM_DISPLAYCHANGE:
                    {
                        var (width, height) = GetPhysicalResolution();
                        PluginContext.Log(pluginName, $"[ResolutionDetector] Resolution changed: {width}x{height}");
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        public void GetCurrentResolution()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var result = new Dictionary<string, string>();
            // Check process 
            try
            {
                var (width, height) = GetPhysicalResolution();
                PluginContext.Log(pluginName, $"[ResolutionDetector] Current Resolution: {width}x{height}");
                result["resolution"] = $"{width}x{height}";
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error detect resolution: {ex.Message}");
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

        #region Windows API Imports

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        public static (int width, int height) GetPhysicalResolution()
        {
            DEVMODE vDevMode = new DEVMODE();
            vDevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            bool success = EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref vDevMode);
            if (success)
            {
                return (vDevMode.dmPelsWidth, vDevMode.dmPelsHeight);
            }
            return (0, 0);
        }

        #endregion
    }
}
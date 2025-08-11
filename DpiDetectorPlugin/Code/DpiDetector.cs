using System.Runtime.InteropServices;
using System.Text.Json;

namespace DpiDetectorPlugin.Code
{
    public class DpiDetector
    {
        #region Singleton
        public static DpiDetector Instance { get; set; }
        public DpiDetector()
        {
            Instance = this;
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        #endregion

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        string pluginName = "DpiDetectorPlugin";

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var result = new Dictionary<string, string>();
            // Check process 
            try
            {
                int dpi = GetDpi();
                PluginContext.Log(pluginName, $"[DpiDetector] Current DPI: {dpi}");
                result["dpi"] = dpi.ToString();
            } catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error detect dpi: {ex.Message}");
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }
        public int GetDpi()
        {
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow != IntPtr.Zero)
            {
                uint dpi = GetDpiForWindow(fgWindow);
                if (dpi != 0) return (int)dpi;
            }
            return 96;
        }

        #region Windows API Imports
        [DllImport("user32.dll")]
        private static extern IntPtr SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        #endregion
    }
}
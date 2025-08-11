using System.Runtime.InteropServices;
using System.Text.Json;

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

        string pluginName = "ResolutionDetectorPlugin";

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var result = new Dictionary<string, string>();
            // Check process 
            try
            {
                String resolution = GetCurrentResolution();
                PluginContext.Log(pluginName, $"[ResolutionDetector] Current Resolution: {resolution}");
                result["resolution"] = resolution;
            } catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error detect resolution: {ex.Message}");
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }
        private string GetCurrentResolution()
        {
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);
            return $"{width}x{height}";
        }

        #region Windows API Imports

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        #endregion
    }
}
using System.Runtime.InteropServices;
using System.Text.Json;

namespace InputMonitorPlugin.Code
{
    public class InputMonitor
    {
        #region Singleton
        public static InputMonitor Instance { get; set; }
        public InputMonitor()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "InputMonitorPlugin";
        private static bool? lastState = null;

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var result = new Dictionary<string, string>();

            // Check process 
            try
            {
                bool isScreenSaverActive = IsScreenSaverActive();
                string currentState = isScreenSaverActive ? "activated" : "deactivated";

                PluginContext.Log(pluginName, $"[InputMonitor] Screen saver: {currentState}");

                lastState = isScreenSaverActive;
                result["status"] = currentState;
            } catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error checking screen saver state: {ex.Message}");
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref bool lpvParam, int flags);

        private const int SPI_GETSCREENSAVERRUNNING = 0x0072;

        private bool IsScreenSaverActive()
        {
            bool isRunning = false;
            SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref isRunning, 0);
            return isRunning;
        }
    }
}
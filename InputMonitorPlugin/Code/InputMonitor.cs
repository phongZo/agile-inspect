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
            var activeTime = setting.eventParams.activeTime;
            var result = new Dictionary<string, string>();

            // Check process 
            try
            {
                bool isScreenSaverEnabled = IsScreenSaverEnabled();
                bool isActive = false;
                string currentState;
                if (isScreenSaverEnabled)
                {
                    bool isScreenSaverActive = IsScreenSaverActive();
                    isActive = isScreenSaverActive;
                    currentState = isActive ? "activated" : "deactivated";
                    PluginContext.Log(pluginName, $"[InputMonitor] Screen saver: {currentState}");
                }
                else
                {
                    uint idleTime = GetIdleTime();
                    isActive = idleTime >= activeTime;
                    currentState = isActive ? "logged" : "unlogged";
                    PluginContext.Log(pluginName, $"[InputMonitor] idle time: {currentState}");
                }

                lastState = isActive;
                result["status"] = currentState;
            } catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error checking screen saver state: {ex.Message}");
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }
        
        private const int SPI_GETSCREENSAVERRUNNING = 0x0072;
        private const int SPI_GETSCREENSAVERACTIVE = 0x0010;

        private bool IsScreenSaverEnabled()
        {
            bool isActive = false;
            SystemParametersInfo(SPI_GETSCREENSAVERACTIVE, 0, ref isActive, 0);

            string scrSaveExe = "";
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    scrSaveExe = key?.GetValue("SCRNSAVE.EXE")?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
            }

            return isActive && !string.IsNullOrEmpty(scrSaveExe);
        }

        private bool IsScreenSaverActive()
        {
            bool isRunning = false;
            SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref isRunning, 0);
            return isRunning;
        }

        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = LASTINPUTINFO.SizeOf;

            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint currentTickCount = GetTickCount();
                uint idleTime = currentTickCount - lastInputInfo.dwTime;
                return idleTime / 1000; // convert milliseconds to second
            }

            return 0;
        }

        #region Windows API Imports

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref bool lpvParam, int flags);
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));
            [MarshalAs(UnmanagedType.U4)]
            public int cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwTime;
        }

        #endregion
    }
}
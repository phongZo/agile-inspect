using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace ScreenLockPlugin
{
    public class ScreenLockDetector
    {
        #region Singleton
        public static ScreenLockDetector Instance { get; set; }
        public ScreenLockDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "ScreenLockPlugin";

        public void Check()
        {
            int timeoutSeconds = GetScreenLockTimeout();
            PluginContext.Log(pluginName, $"[ScreenLock] Screen lock timeout: {timeoutSeconds} seconds");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = timeoutSeconds
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private int GetScreenLockTimeout()
        {
            try
            {
                // Check power settings for screen timeout (in seconds)
                // HKEY_CURRENT_USER\Control Panel\Desktop\ScreenSaveTimeOut
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key != null)
                {
                    var value = key.GetValue("ScreenSaveTimeOut");
                    if (value != null && int.TryParse(value.ToString(), out int timeout))
                    {
                        return timeout;
                    }
                }

                // Alternative: Check power policy for lock timeout
                // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System
                using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                if (policyKey != null)
                {
                    var inactivityValue = policyKey.GetValue("InactivityTimeoutSecs");
                    if (inactivityValue != null && int.TryParse(inactivityValue.ToString(), out int inactivityTimeout))
                    {
                        return inactivityTimeout;
                    }
                }

                return 0; // 0 means not configured or disabled
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ScreenLock] Error getting timeout: {ex.Message}");
                return -1;
            }
        }
    }
}

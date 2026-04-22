using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace DevModePlugin
{
    public class DevModeDetector
    {
        #region Singleton
        public static DevModeDetector Instance { get; set; }
        public DevModeDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "DevModePlugin";

        public void Check()
        {
            bool isEnabled = IsDeveloperModeEnabled();
            PluginContext.Log(pluginName, $"[DevMode] Developer mode enabled: {isEnabled}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isEnabled
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private bool IsDeveloperModeEnabled()
        {
            try
            {
                // 32-bit process (win-x86): Registry.LocalMachine follows WOW6432Node. Developer Mode lives under
                // the 64-bit hive: HKLM\SOFTWARE\...\AppModelUnlock — must use RegistryView.Registry64.
                // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var devModeKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
                if (devModeKey != null)
                {
                    // AllowDevelopmentWithoutDevLicense: 1 = Developer mode enabled
                    var value = devModeKey.GetValue("AllowDevelopmentWithoutDevLicense");
                    if (value != null && int.TryParse(value.ToString(), out int enabled))
                    {
                        return enabled == 1;
                    }

                    // Alternative: AllowAllTrustedApps
                    var trustedApps = devModeKey.GetValue("AllowAllTrustedApps");
                    if (trustedApps != null && int.TryParse(trustedApps.ToString(), out int trusted))
                    {
                        return trusted == 1;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[DevMode] Error checking status: {ex.Message}");
                return false;
            }
        }
    }
}

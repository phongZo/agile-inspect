using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AutoUpdatePlugin
{
    public class AutoUpdateDetector
    {
        #region Singleton
        public static AutoUpdateDetector Instance { get; set; }
        public AutoUpdateDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "AutoUpdatePlugin";

        public void Check()
        {
            bool isDisabled = IsAutoUpdateDisabled();
            PluginContext.Log(pluginName, $"[AutoUpdate] Auto-update disabled: {isDisabled}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isDisabled
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private bool IsAutoUpdateDisabled()
        {
            try
            {
                // Check Windows Update settings
                // HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU
                using var auKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
                if (auKey != null)
                {
                    // NoAutoUpdate = 1 means auto-update is disabled
                    var noAutoUpdate = auKey.GetValue("NoAutoUpdate");
                    if (noAutoUpdate != null && int.TryParse(noAutoUpdate.ToString(), out int value) && value == 1)
                    {
                        return true;
                    }

                    // AUOptions: 1 = Disabled, 2 = Notify, 3 = Auto download, 4 = Auto install
                    var auOptions = auKey.GetValue("AUOptions");
                    if (auOptions != null && int.TryParse(auOptions.ToString(), out int optValue) && optValue == 1)
                    {
                        return true;
                    }
                }

                // Alternative check in Windows Update service settings
                using var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update");
                if (wuKey != null)
                {
                    var auOptions = wuKey.GetValue("AUOptions");
                    if (auOptions != null && int.TryParse(auOptions.ToString(), out int optValue) && optValue == 1)
                    {
                        return true;
                    }
                }

                return false; // Auto-update is enabled
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[AutoUpdate] Error checking status: {ex.Message}");
                return false;
            }
        }
    }
}

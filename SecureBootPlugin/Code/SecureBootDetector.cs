using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace SecureBootPlugin
{
    public class SecureBootDetector
    {
        #region Singleton
        public static SecureBootDetector Instance { get; set; }
        public SecureBootDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "SecureBootPlugin";

        public void Check()
        {
            var status = GetSecureBootStatus();
            PluginContext.Log(pluginName, $"[SecureBoot] SecureBoot: {status.SecureBootEnabled}, TPM: {status.TpmPresent}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = new JObject
                {
                    ["secureBootEnabled"] = status.SecureBootEnabled,
                    ["tpmPresent"] = status.TpmPresent
                }
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private (bool SecureBootEnabled, bool TpmPresent) GetSecureBootStatus()
        {
            bool secureBootEnabled = false;
            bool tpmPresent = false;

            try
            {
                // Check Secure Boot status from registry
                // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State
                using var secureBootKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (secureBootKey != null)
                {
                    var value = secureBootKey.GetValue("UEFISecureBootEnabled");
                    if (value != null && int.TryParse(value.ToString(), out int enabled))
                    {
                        secureBootEnabled = enabled == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[SecureBoot] Error checking SecureBoot: {ex.Message}");
            }

            try
            {
                // Check TPM status
                // Try WMI approach via registry or check TPM service
                using var tpmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Tpm");
                if (tpmKey != null)
                {
                    tpmPresent = true; // If the key exists, TPM is likely present
                }

                // Alternative check
                using var tpmDeviceKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM");
                if (tpmDeviceKey != null)
                {
                    var start = tpmDeviceKey.GetValue("Start");
                    if (start != null)
                    {
                        tpmPresent = true;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[SecureBoot] Error checking TPM: {ex.Message}");
            }

            return (secureBootEnabled, tpmPresent);
        }
    }
}

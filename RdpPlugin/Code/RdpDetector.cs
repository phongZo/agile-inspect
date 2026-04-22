using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace RdpPlugin
{
    public class RdpDetector
    {
        #region Singleton
        public static RdpDetector Instance { get; set; }
        public RdpDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "RdpPlugin";

        public void Check()
        {
            bool isEnabled = IsRdpEnabled();
            PluginContext.Log(pluginName, $"[Rdp] Remote Desktop enabled: {isEnabled}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isEnabled
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private bool IsRdpEnabled()
        {
            try
            {
                // Check Remote Desktop settings
                // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server
                using var tsKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
                if (tsKey != null)
                {
                    // fDenyTSConnections: 0 = RDP enabled, 1 = RDP disabled
                    var value = tsKey.GetValue("fDenyTSConnections");
                    if (value != null && int.TryParse(value.ToString(), out int denyConnections))
                    {
                        return denyConnections == 0;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[Rdp] Error checking status: {ex.Message}");
                return false;
            }
        }
    }
}

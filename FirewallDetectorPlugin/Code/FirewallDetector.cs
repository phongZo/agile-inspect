using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using System.Management;

namespace FirewallDetectorPlugin
{
    public class FirewallDetector
    {
        #region Singleton
        public static FirewallDetector Instance { get; set; }
        public FirewallDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "FirewallDetectorPlugin";

        public void CheckFirewall()
        {
            bool isOn = IsFirewallEnabled();
            string value = isOn ? "ON" : "OFF";
            PluginContext.Log(pluginName, $"[FirewallDetector] Firewall detected: {(isOn ? "on" : "off")}");

            // save last state and check rule
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), value);

            var resultObj = new Dictionary<string, object>
            {
                ["isOn"] = isOn
            };
            string jsonResult = JsonConvert.SerializeObject(resultObj);
            PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

        private bool IsFirewallEnabled()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\StandardCimv2",
                    "SELECT * FROM MSFT_NetFirewallProfile"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        ushort enabledValue = Convert.ToUInt16(obj["Enabled"]);
                        if (enabledValue != 1)
                            return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[FirewallDetector] Error: {ex.Message}");
                return false;
            }
        }
    }
}

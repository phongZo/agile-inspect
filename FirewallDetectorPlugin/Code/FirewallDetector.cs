using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private ManagementEventWatcher? EventWatcher;

        string pluginName = "FirewallDetectorPlugin";

        public void StartWatcher()
        {
            try
            {
                string queryString = @"
                    SELECT * FROM __InstanceModificationEvent
                    WITHIN 1
                    WHERE TargetInstance ISA 'MSFT_NetFirewallProfile'";

                WqlEventQuery query = new WqlEventQuery(queryString);
                EventWatcher = new ManagementEventWatcher(@"root\StandardCimv2", query.QueryString);

                EventWatcher.EventArrived += (sender, args) =>
                {
                    try
                    {
                        CheckFirewall();
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log(pluginName, $"[Firewall] Watcher handler error: {ex.Message}");
                    }
                };

                EventWatcher.Start();
                PluginContext.Log(pluginName, "[Firewall] Realtime watcher started.");

                // Check firewall state immediately on startup
                CheckFirewall();
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[Firewall] Failed to start realtime watcher: {ex.Message}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (EventWatcher != null)
                {
                    EventWatcher.Stop();
                    EventWatcher.Dispose();
                    EventWatcher = null;
                    PluginContext.Log(pluginName, "[Firewall] Realtime watcher stopped.");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[Firewall] Error stopping realtime watcher: {ex.Message}");
            }
        }

        public void CheckFirewall()
        {
            bool isOn = IsFirewallEnabled();
            string value = isOn ? "ON" : "OFF";
            PluginContext.Log(pluginName, $"[FirewallDetector] Firewall detected: {(isOn ? "on" : "off")}");

            // save last state and check rule
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), value);

            var resultObj = new JObject            {
                ["isOn"] = isOn
            };
            PluginContext.SendDetectionResult(pluginName, resultObj);
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

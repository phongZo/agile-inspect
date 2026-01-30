using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace VpnDetectorPlugin
{
    public class VpnDetector
    {
        #region Singleton
        public static VpnDetector Instance { get; set; }
        public VpnDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "VpnDetectorPlugin";

        public void CheckVpn()
        {
            bool isOn = isVpnEnabled();
            PluginContext.Log(pluginName, $"[VpnDetector] Vpn detected: {(isOn ? "on" : "off")}");
            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isOn
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private bool isVpnEnabled()
        {
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface Interface in interfaces)
                {
                    if (Interface.OperationalStatus == OperationalStatus.Up && Interface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        if (Interface.NetworkInterfaceType == NetworkInterfaceType.Ppp 
                            || Interface.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                            || (int)Interface.NetworkInterfaceType == 53)
                        {
                            PluginContext.Log(pluginName, $"[VpnDetector] Detect: {Interface.Name + " " + Interface.NetworkInterfaceType.ToString() + " " + Interface.Description}");
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}

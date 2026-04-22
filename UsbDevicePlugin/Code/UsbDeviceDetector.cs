using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace UsbDevicePlugin
{
    public class UsbDeviceDetector
    {
        #region Singleton
        public static UsbDeviceDetector Instance { get; set; }
        public UsbDeviceDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "UsbDevicePlugin";

        public void Check()
        {
            var connectedDrives = GetRemovableDriveList();
            bool connected = connectedDrives.Count > 0;

            PluginContext.Log(pluginName, $"[UsbDevice] USB removable storage connected: {connected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = connected,
                ["driveList"] = JToken.FromObject(connectedDrives)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> GetRemovableDriveList()
        {
            var result = new List<string>();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Removable) continue;
                    if (!drive.IsReady) continue;

                    var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "NoLabel" : drive.VolumeLabel;
                    result.Add($"{drive.Name} ({label})");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[UsbDevice] Drive scan error: {ex.Message}");
            }

            return result;
        }
    }
}

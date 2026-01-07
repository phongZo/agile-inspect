using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using System.Management;

namespace ExternalDiskDetectorPlugin
{
    public class ExternalDiskDetector
    {
        #region Singleton
        public static ExternalDiskDetector Instance { get; set; }
        public ExternalDiskDetector()
        {
            Instance = this;
        }
        #endregion

        private ManagementEventWatcher? EventWatcher;

        string pluginName = "ExternalDiskDetectorPlugin";

        public void StartWatcher()
        {
            try
            {
                // Win32_VolumeChangeEvent:
                // EventType = 2 -> insert
                // EventType = 3 -> remove
                WqlEventQuery query = new WqlEventQuery(@"SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 OR EventType = 3");
                EventWatcher = new ManagementEventWatcher(@"root\CIMV2", query.QueryString);
                EventWatcher.EventArrived += (sender, args) =>
                {
                    try
                    {
                        int eventType = Convert.ToInt32(args.NewEvent["EventType"]);
                        string driveName = args.NewEvent["DriveName"]?.ToString() ?? "Unknown";

                        if (eventType == 2)
                        {
                            PluginContext.Log(pluginName, $"[ExternalDiskDetector] External storage inserted: {driveName}");
                        }
                        else if (eventType == 3)
                        {
                            PluginContext.Log(pluginName, $"[ExternalDiskDetector] External storage removed: {driveName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log(pluginName, $"[ExternalDiskDetector] Watcher handler error: {ex.Message}");
                    }
                };

                EventWatcher.Start();
                PluginContext.Log(pluginName, "[ExternalDiskDetector] Realtime watcher started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ExternalDiskDetector] Failed to start watcher: {ex.Message}");
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
                    PluginContext.Log(pluginName, "[ExternalDiskDetector] Realtime watcher stopped.");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ExternalDiskDetector] Error stopping realtime watcher: {ex.Message}");
            }
        }

        public void CheckExternalDisk()
        {
            try
            {
                bool isPlugExternalHardDisk = false;
                using var driveSearcher = new ManagementObjectSearcher(
                    @"root\CIMV2",
                    "SELECT DeviceID, Model, SerialNumber, InterfaceType, MediaType FROM Win32_DiskDrive");

                foreach (ManagementObject disk in driveSearcher.Get())
                {
                    string deviceId = disk["DeviceID"]?.ToString() ?? "UnknownDevice";
                    string model = (disk["Model"]?.ToString() ?? "UnknownModel").Trim();
                    string serial = (disk["SerialNumber"]?.ToString() ?? "").Trim();
                    string interfaceType = (disk["InterfaceType"]?.ToString() ?? "").Trim();
                    string mediaType = (disk["MediaType"]?.ToString() ?? "").Trim().ToLowerInvariant();

                    if (interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase)
                        || mediaType.Contains("external") || mediaType.Contains("removable"))
                    {
                        isPlugExternalHardDisk = true;
                        PluginContext.Log(pluginName, $"[ExternalDiskDetector] External hard disk: Model={model}, Serial={serial}, DeviceID={deviceId}");
                    }
                }
                string value = isPlugExternalHardDisk ? "PLUG" : "UNPLUG";
                PluginContext.Log(pluginName, $"[ExternalDiskDetector] Plug external hard disk: {(isPlugExternalHardDisk)}");

                // save last state and check rule
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), value);

                var resultObj = new Dictionary<string, object>
                {
                    ["isPlugExternalHardDisk"] = isPlugExternalHardDisk
                };
                string jsonResult = JsonConvert.SerializeObject(resultObj);
                PluginContext.SendDetectionResult(pluginName, jsonResult);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ExternalDiskDetector] CheckExternalDisk error: {ex.Message}");
            }
        }
    }
}

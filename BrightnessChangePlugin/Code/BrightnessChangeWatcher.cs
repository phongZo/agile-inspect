
using System.Management;
using System.Text.Json;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;

namespace BrightnessChangePlugin.Code
{
    public class BrightnessChangeWatcher
    {
        #region Singleton
        public static BrightnessChangeWatcher Instance { get; set; }
        public BrightnessChangeWatcher()
        {
            Instance = this;
        }
        #endregion

        private ManagementEventWatcher? EventWatcher;
        public string Name => "BrightnessChangePlugin";

        public void StartWatcher()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                EventWatcher = new ManagementEventWatcher("root\\WMI", query.QueryString);
                EventWatcher.EventArrived += (sender, args) =>
                {
                    var brightness = args.NewEvent["Brightness"];
                    PluginContext.Log(Name, $"[Brightness] Brightness changed: {brightness}");
                };
                EventWatcher.Start();
                PluginContext.Log(Name, "[Brightness] Realtime watcher started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Failed to start realtime watcher: {ex.Message}");
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
                    PluginContext.Log(Name, "[Brightness] Realtime watcher stopped.");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Error stopping realtime watcher: {ex.Message}");
            }
        }

        public void GetCurrentBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int brightness = Convert.ToInt32(obj["CurrentBrightness"]);
                    PluginContext.Log(Name, $"[Brightness] Current brightness: {brightness}");
                    var resultObj = new Dictionary<string, object>
                    {
                        ["brightness"] = brightness
                    };
                    string jsonResult = JsonConvert.SerializeObject(resultObj);
                    RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(Name), (long)brightness);
                    PluginContext.SendDetectionResult(Name, jsonResult);
                    return;
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Failed to get current brightness: {ex.Message}");
                try
                {
                    using var monitors = MonitorManager.GetAllMonitors();
                    var result = monitors
                        .Select(m => new { brightness = m.CurrentBrightness })
                        .Max();
                    var resultObj = new Dictionary<string, object>
                    {
                        ["brightness"] = result.brightness
                    };
                    string jsonResult = JsonConvert.SerializeObject(result);
                    PluginContext.Log(Name, $"[Brightness] Get brightness from external monitor");
                    PluginContext.Log(Name, $"[Brightness] Current brightness: {result.brightness}");
                    RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(Name), ((long)result.brightness));
                    PluginContext.SendDetectionResult(Name, jsonResult);
                }
                catch (Exception)
                {
                    PluginContext.Log(Name, $"[Brightness] Default brightness: {50}");
                    RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(Name), ((long)50));
                    var resultObj = new Dictionary<string, object>
                    {
                        ["brightness"] = 50
                    };
                    string jsonResult = JsonConvert.SerializeObject(resultObj);
                    PluginContext.SendDetectionResult(Name, jsonResult);
                    return;
                }

                return;
            }

            PluginContext.Log(Name, "[Brightness] Brightness info not found.");
        }
    }
}

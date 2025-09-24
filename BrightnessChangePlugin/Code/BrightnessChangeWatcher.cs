using AgileInspect.Code.MonitorManagement;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using System.Management;
using System.Text.Json;

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
            var result = new List<(string deviceName, int brightness)>();
            int brightness = 0;

            // get main monitor brightness (laptop)
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    brightness = Convert.ToInt32(obj["CurrentBrightness"]);
                    result.Add(("main screen", brightness));
                    break;
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Failed to get current main screen brightness: {ex.Message}");
            }

            // get all monitor brightness (support DDC/CI)
            try
            {
                var monitors = MultipleMonitors.GetBrightnessMonitors(brightness);
                foreach (var m in monitors)
                {
                    result.Add((m.deviceName, m.brightness));
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Failed to get current brightness: {ex.Message}");
            }

            if (result.Count > 0)
            {
                var monitorInfo = string.Join(", ", result.Select(m => $"{m.deviceName}: {m.brightness}"));
                PluginContext.Log(Name, $"{monitorInfo}");

                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(Name), (long) result[0].brightness);

                var brightnessArray = result.Select(m => new Dictionary<string, object>
                {
                    { "monitor", m.deviceName },
                    { "value", m.brightness }
                }).ToList();

                var resultObj = new Dictionary<string, object>
                {
                    { "brightness", brightnessArray }
                };
                string jsonResult = JsonSerializer.Serialize(resultObj);
                PluginContext.SendDetectionResult(Name, jsonResult);
            }
        }
    }
}

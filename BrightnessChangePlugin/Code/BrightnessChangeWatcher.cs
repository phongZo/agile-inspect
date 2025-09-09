using System.Management;

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
                    return;
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[Brightness] Failed to get current brightness: {ex.Message}");
                return;
            }

            PluginContext.Log(Name, "[Brightness] Brightness info not found.");
        }
    }
}

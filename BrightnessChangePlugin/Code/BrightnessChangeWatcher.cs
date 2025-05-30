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

        public void StartWatcher()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                EventWatcher = new ManagementEventWatcher("root\\WMI", query.QueryString);
                EventWatcher.EventArrived += (sender, args) =>
                {
                    var brightness = args.NewEvent["Brightness"];
                    DebugLog.WriteLine($"[Brightness] Brightness changed: {brightness}");
                };
                EventWatcher.Start();
                DebugLog.WriteLine("[Brightness] Realtime watcher started.");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[Brightness] Failed to start realtime watcher: {ex.Message}");
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
                    DebugLog.WriteLine("[Brightness] Realtime watcher stopped.");
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[Brightness] Error stopping realtime watcher: {ex.Message}");
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
                    DebugLog.WriteLine($"[Brightness] Current brightness: {brightness}");
                    return;
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[Brightness] Failed to get current brightness: {ex.Message}");
                return;
            }

            DebugLog.WriteLine("[Brightness] Brightness info not found.");
        }
    }
}

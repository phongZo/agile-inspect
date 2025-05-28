using AgileInspect.Code.PluginContracts;
using System.Management;
namespace BrightnessChangePlugin
{
    public class BrightnessChangePlugin : IBrightnessChangePlugin
    {
        private ManagementEventWatcher EventWatcher;
        public Permission Permission { get; set; } = new Permission();

        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.WriteLine("ScreenBrightnessPlugin initialized.");
        }

        public void Start()
        {
            DebugLog.WriteLine("ScreenBrightnessPlugin started.: " + GetBrightness());

            try
            {
                // Listen to brightness change events
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                EventWatcher = new ManagementEventWatcher("root\\WMI", query.QueryString);
                EventWatcher.EventArrived += (sender, args) =>
                {
                    var brightness = args.NewEvent["Brightness"];
                    DebugLog.WriteLine($"Brightness changed: {brightness}");
                };
                EventWatcher.Start();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to start brightness monitor: {ex.Message}");
            }
        }

        public void Stop()
        {
            DebugLog.WriteLine("ScreenBrightnessPlugin stopped.");

            if (EventWatcher != null)
            {
                EventWatcher.Stop();
                EventWatcher.Dispose();
                EventWatcher = null;
            }
        }

        public static int GetBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToInt32(obj["CurrentBrightness"]);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to get brightness.", ex);
            }

            throw new NotSupportedException("Brightness info not found.");
        }
    }

}

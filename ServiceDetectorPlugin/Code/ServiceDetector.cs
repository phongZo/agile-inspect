using System.Diagnostics;

namespace ServiceDetectorPlugin.Code
{
    public class ServiceDetector
    {
        #region Singleton
        public static ServiceDetector Instance { get; set; }
        public ServiceDetector()
        {
            Instance = this;
        }
        #endregion

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();

            List<string> services = setting.EventParams.Services.ToList();

            if (services.Count == 0)
            {
                DebugLog.WriteLine("No services specified for monitoring.");
                return;
            }

            foreach (var serviceName in services)
            {
                try
                {
                    bool isRunning = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(serviceName)).Any();
                    DebugLog.WriteLine($"Service '{serviceName}': {(isRunning ? "Running" : "Not running")}");
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Error checking service '{serviceName}': {ex.Message}");
                }
            }
        }
    }
}
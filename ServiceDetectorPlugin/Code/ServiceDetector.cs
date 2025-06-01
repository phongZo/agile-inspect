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
        string pluginName = "ServiceDetectorPlugin";

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();

            List<string> services = setting.EventParams.Services.ToList();

            if (services.Count == 0)
            {
                PluginContext.Log(pluginName, "No services specified for monitoring.");
                return;
            }

            foreach (var serviceName in services)
            {
                try
                {
                    bool isRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(serviceName)).Any();
                    PluginContext.Log(pluginName,$"Service '{serviceName}': {(isRunning ? "Running" : "Not running")}");
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName,$"Error checking service '{serviceName}': {ex.Message}");
                }
            }
        }
    }
}
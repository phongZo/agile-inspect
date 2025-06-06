using System.Diagnostics;
using System.Text.Json;

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
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            List<string> services = setting.eventParams.services.ToList();

            if (services.Count == 0)
            {
                PluginContext.Log(pluginName, "No services specified for monitoring.");
                return;
            }

            var result = new Dictionary<string, string>();

            foreach (var serviceName in services)
            {
                try
                {
                    string key = serviceName.ToLower();
                    bool isRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(serviceName)).Any();
                    string status = isRunning ? "on" : "off";

                    PluginContext.Log(pluginName, $"Service '{serviceName}': {status}");
                    result[key] = status;
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"Error checking service '{serviceName}': {ex.Message}");
                    result[serviceName.ToLower()] = "error";
                }
            }

            string jsonResult = JsonSerializer.Serialize(result);
            PluginContext.SendDetectionResult(pluginName, jsonResult);
        }
    }
}
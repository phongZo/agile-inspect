using System.ServiceProcess;
using System.Text.Json;

namespace ServiceMonitorPlugin.Code
{
    public class ServiceMonitor
    {
        #region Singleton
        public static ServiceMonitor Instance { get; set; }
        public ServiceMonitor()
        {
            Instance = this;
        }
        #endregion
        string pluginName = "ServiceMonitoringPlugin";

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var services = setting.eventParams.services?.ToList() ?? new List<string>();

            if (services.Count == 0)
            {
                PluginContext.Log(pluginName, "No services or processes specified for monitoring.");
                return;
            }

            var result = new Dictionary<string, string>();

            // Check service
            foreach (var serviceName in services)
            {
                try
                {
                    string key = $"service:{serviceName.ToLower()}";
                    using var controller = new ServiceController(serviceName);
                    string status = controller.Status == ServiceControllerStatus.Running ? "on" : "off";

                    PluginContext.Log(pluginName, $"Service '{serviceName}': {status}");
                    result[key] = status;
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"Error checking service '{serviceName}': {ex.Message}");
                    result[$"service:{serviceName.ToLower()}"] = "error";
                }
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

    }
}
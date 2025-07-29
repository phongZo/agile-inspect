using System.Diagnostics;
using System.Text.Json;

namespace ProcessMonitorPlugin.Code
{
    public class ProcessMonitor
    {
        #region Singleton
        public static ProcessMonitor Instance { get; set; }
        public ProcessMonitor()
        {
            Instance = this;
        }
        #endregion
        string pluginName = "ProcessMonitorPlugin";

        public void CheckServices()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var services = setting.eventParams.services?.ToList() ?? new List<string>();
            var processes = setting.eventParams.processes?.ToList() ?? new List<string>();

            if (processes.Count == 0 && services.Count == 0)
            {
                PluginContext.Log(pluginName, "No services or processes specified for monitoring.");
                return;
            }

            var result = new Dictionary<string, string>();

            // Check process
            foreach (var processName in processes)
            {
                try
                {
                    string key = $"process:{processName.ToLower()}";
                    bool isRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).Any();
                    string status = isRunning ? "on" : "off";

                    PluginContext.Log(pluginName, $"Process '{processName}': {status}");
                    result[key] = status;
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"Error checking process '{processName}': {ex.Message}");
                    result[$"process:{processName.ToLower()}"] = "error";
                }
            }

            string jsonResult = JsonSerializer.Serialize(result);
            //PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

    }
}
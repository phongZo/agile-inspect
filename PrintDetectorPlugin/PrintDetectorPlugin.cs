using AgileInspect.Code.PluginContracts;
using System;
using System.Text.Json;
using System.Management;
using System.Runtime.InteropServices;
using PrintDetectorPlugin.Code.Settings;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PrintDetectorPlugin
{
    public class PrintDetectorPlugin : IPrintDetectorPlugin
    {
        public string pluginName => "PrintDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        private ManagementEventWatcher _watcher;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public void Initialize()
        {
            PluginContext.Log(pluginName, "Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
             var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            if (!string.IsNullOrEmpty(triggerParamsJson))
            {
                try
                {
                    var triggerParams = JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson);
                    if (triggerParams != null) setting.triggerParams = triggerParams;
                }
                catch { }
            }
            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            StoreCfgJson.Instance.eventSetting = setting;
            PluginContext.Log(pluginName, "Parameters is set");
        }

        public void Start()
        {
            try 
            {
                PluginContext.Log(pluginName, "Starting Print Watcher...");
                Stop(); // Ensure clean start

                var query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PrintJob'");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += Watcher_EventArrived;
                _watcher.Start();
                PluginContext.Log(pluginName, "Print Watcher started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Failed to start Print Watcher: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                try 
                {
                    _watcher.Stop();
                    _watcher.Dispose();
                }
                catch {}
                _watcher = null;
                PluginContext.Log(pluginName, "Print Watcher stopped.");
            }
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var newEvent = e.NewEvent;
                var targetInstance = (ManagementBaseObject)newEvent["TargetInstance"];
                
                string documentName = targetInstance["Document"]?.ToString() ?? "Unknown";
                string printerName = targetInstance["Name"]?.ToString() ?? "Unknown";
                string owner = targetInstance["Owner"]?.ToString() ?? "Unknown";

                // Heuristic detection removed as per request.
                // Standard WMI/PrintSpooler API does not provide Source Process ID or Full File Path.
                
                PluginContext.Log(pluginName, $"Print detected: '{documentName}' on {printerName}");
                
                var result = new JObject
                {
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["file_name"] = documentName,
                    ["printer_name"] = printerName,
                    ["owner"] = owner
                };

                PluginContext.SendDetectionResult(pluginName, result);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error handling print event: {ex.Message}");
            }
        }
    }
}

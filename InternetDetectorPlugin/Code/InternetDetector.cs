using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;

namespace InternetDetectorPlugin
{
    public class InternetDetector
    {
        #region Singleton
        public static InternetDetector Instance { get; set; }
        public InternetDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "InternetDetectorPlugin";
        private bool _started;

        private Timer? _debounceTimer;
        private readonly object _lock = new object();

        public void StartWatcher()
        {
            try
            {
                if (_started) return;

                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

                _started = true;

                PluginContext.Log(pluginName, "[InternetDetector] Realtime watcher started.");

                ScheduleCheck();
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[InternetDetector] Failed to start watcher: {ex.Message}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (!_started) return;

                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

                lock (_lock)
                {
                    _debounceTimer?.Dispose();
                    _debounceTimer = null;
                }

                _started = false;
                PluginContext.Log(pluginName, "[InternetDetector] Realtime watcher stopped.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[InternetDetector] Error stopping watcher: {ex.Message}");
            }
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            ScheduleCheck();
        }

        private void ScheduleCheck()
        {
            const int delayMs = 1000;

            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        CheckAllowInternet();
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log(pluginName, $"[InternetDetector] CheckAllowInternet failed: {ex.Message}");
                    }
                }, null, delayMs, Timeout.Infinite);
            }
        }

        public void CheckAllowInternet()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var allowedInternetSsids = setting.eventParams.allowedInternetSsids?.ToList() ?? new List<string>();
            bool isAllowed = IsInternetAllowed(allowedInternetSsids);
            PluginContext.Log(pluginName, $"[InternetDetector] Internet allowed: {isAllowed}");
            var resultObj = new JObject            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isAllowed
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private bool IsInternetAllowed(List<string> allowedInternetSsids)
        {
            try
            {
                string ssid = GetInternetSSID();
                PluginContext.Log(pluginName, $"[InternetDetector] Current SSID: {ssid}");
                if (string.IsNullOrWhiteSpace(ssid))
                    return false;

                return allowedInternetSsids.Contains(ssid);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[InternetDetector] Error: {ex.Message}");
                return false;
            }
        }
        private string GetInternetSSID()
        {
            try
            {
                // Use netsh command to get SSID
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = "wlan show interfaces",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse SSID from output
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) 
                        && trimmed.Contains(":"))
                    {
                        int index = trimmed.IndexOf(':');
                        if (index > -1 && index + 1 < trimmed.Length)
                        {
                            return trimmed.Substring(index + 1).Trim();
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[InternetDetector] Error: {ex.Message}");
                return null;
            }
        }
    }
}

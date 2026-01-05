using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;

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

        public void CheckAllowInternet()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var allowedInternetSsids = setting.eventParams.allowedInternetSsids?.ToList() ?? new List<string>();
            bool isAllowed = IsInternetAllowed(allowedInternetSsids);
            string value = isAllowed ? "ALLOW" : "NOT ALLOW";
            PluginContext.Log(pluginName, $"[InternetDetector] Internet allowed: {isAllowed}");

            // save last state and check rule
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), value);

            var resultObj = new Dictionary<string, object>
            {
                ["isAllowed"] = isAllowed
            };
            string jsonResult = JsonConvert.SerializeObject(resultObj);
            PluginContext.SendDetectionResult(pluginName, jsonResult);
        }

        private bool IsInternetAllowed(List<string> allowedInternetSsids)
        {
            try
            {
                string ssid = GetInternetSSID();
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

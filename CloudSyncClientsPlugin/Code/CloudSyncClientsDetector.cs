using System.Diagnostics;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace CloudSyncClientsPlugin
{
    public class CloudSyncClientsDetector
    {
        #region Singleton
        public static CloudSyncClientsDetector Instance { get; set; }
        public CloudSyncClientsDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "CloudSyncClientsPlugin";

        private static readonly string[] DefaultProcesses =
        [
            "dropbox",
            "googledrivesync",
            "googledrive",
            "googledrivefs",
            "onedrive",
            "box",
            "boxdrive",
            "boxsync",
            "icloud",
            "iclouddrive",
            "nextcloud",
            "syncthing",
            "megasync"
        ];

        public void Check()
        {
            var activeClients = GetActiveClients();
            bool detected = activeClients.Count > 0;

            PluginContext.Log(pluginName, $"[CloudSyncClients] Active cloud sync clients: {detected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                ["appList"] = JToken.FromObject(activeClients)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> GetActiveClients()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredProcessSet = GetConfiguredProcessSet();

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    var processName = process.ProcessName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(processName)) continue;
                    var normalized = NormalizeProcessName(processName);

                    if (configuredProcessSet.Contains(normalized))
                    {
                        result.Add(processName);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[CloudSyncClients] Process scan error: {ex.Message}");
            }

            return result.OrderBy(x => x).ToList();
        }

        private static string NormalizeProcessName(string processName)
        {
            return processName
                .Trim()
                .ToLowerInvariant()
                .Replace(".exe", "")
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");
        }

        private HashSet<string> GetConfiguredProcessSet()
        {
            var configured = StoreCfgJson.Instance?.eventSetting?.eventParams?.processes;
            var selected = (configured != null && configured.Length > 0) ? configured : DefaultProcesses;

            return selected
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizeProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}

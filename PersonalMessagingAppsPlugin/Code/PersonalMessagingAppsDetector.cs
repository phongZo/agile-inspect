using System.Diagnostics;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace PersonalMessagingAppsPlugin
{
    public class PersonalMessagingAppsDetector
    {
        #region Singleton
        public static PersonalMessagingAppsDetector Instance { get; set; }
        public PersonalMessagingAppsDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "PersonalMessagingAppsPlugin";

        private static readonly string[] DefaultProcesses =
        [
            "whatsapp",
            "telegram",
            "discord",
            "line",
            "lineapp",
            "wechat",
            "viber",
            "zalo",
            "skype",
            "signal"
        ];

        public void Check()
        {
            var activeApps = GetActiveMessagingApps();
            bool detected = activeApps.Count > 0;

            PluginContext.Log(pluginName, $"[PersonalMessagingApps] Personal messaging apps open: {detected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                ["appList"] = JToken.FromObject(activeApps)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> GetActiveMessagingApps()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredProcessSet = GetConfiguredProcessSet();
            var detectBackground = StoreCfgJson.Instance?.eventSetting?.eventParams?.detectBackground ?? false;

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    var processName = process.ProcessName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(processName)) continue;
                    var normalized = NormalizeProcessName(processName);

                    if (configuredProcessSet.Contains(normalized))
                    {
                        if (!detectBackground && !HasVisibleWindow(process))
                        {
                            continue;
                        }
                        result.Add(processName);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[PersonalMessagingApps] Process scan error: {ex.Message}");
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

        private static bool HasVisibleWindow(Process process)
        {
            try
            {
                return process.MainWindowHandle != IntPtr.Zero
                    && !string.IsNullOrWhiteSpace(process.MainWindowTitle);
            }
            catch
            {
                return false;
            }
        }
    }
}

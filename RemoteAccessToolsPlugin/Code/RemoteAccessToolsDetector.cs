using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace RemoteAccessToolsPlugin
{
    public class RemoteAccessToolsDetector
    {
        #region Singleton
        public static RemoteAccessToolsDetector Instance { get; set; }
        public RemoteAccessToolsDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "RemoteAccessToolsPlugin";

        private static readonly string[] RemoteToolKeywords =
        [
            "teamviewer",
            "anydesk",
            "rustdesk",
            "ultravnc",
            "tightvnc",
            "realvnc",
            "logmein",
            "splashtop",
            "connectwise control",
            "screenconnect",
            "remote utilities",
            "aeroadmin"
        ];
        private static readonly Dictionary<string, string> RemoteToolProcessMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["rustdesk"] = "RustDesk",
            ["teamviewer"] = "TeamViewer",
            ["anydesk"] = "AnyDesk",
            ["tvnserver"] = "TightVNC",
            ["winvnc"] = "VNC",
            ["vncserver"] = "VNC",
            ["uvnc"] = "UltraVNC",
            ["rutserv"] = "Remote Utilities",
            ["rfusclient"] = "Remote Utilities",
            ["aeroadmin"] = "AeroAdmin",
            ["splashtop"] = "Splashtop",
            ["screenconnect"] = "ConnectWise Control",
            ["logmein"] = "LogMeIn"
        };

        public void Check()
        {
            var installedApps = GetInstalledRemoteTools();
            bool detected = installedApps.Count > 0;

            PluginContext.Log(pluginName, $"[RemoteAccessTools] Installed remote access tools: {detected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                ["appList"] = JToken.FromObject(installedApps)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> GetInstalledRemoteTools()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var uninstallPath in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                ScanUninstallKey(Registry.LocalMachine, uninstallPath, result);
                ScanUninstallKey(Registry.CurrentUser, uninstallPath, result);
            }

            ScanRunningProcesses(result);
            return result.OrderBy(x => x).ToList();
        }

        private void ScanRunningProcesses(HashSet<string> result)
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    var processName = process.ProcessName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        continue;
                    }

                    var normalized = processName.ToLowerInvariant();
                    foreach (var kv in RemoteToolProcessMap)
                    {
                        if (normalized.Contains(kv.Key))
                        {
                            result.Add(kv.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[RemoteAccessTools] Process scan error: {ex.Message}");
            }
        }

        private void ScanUninstallKey(RegistryKey hive, string subKeyPath, HashSet<string> result)
        {
            try
            {
                using var key = hive.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var appKeyName in key.GetSubKeyNames())
                {
                    using var appKey = key.OpenSubKey(appKeyName);
                    var displayName = appKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;

                    var lower = displayName.ToLowerInvariant();
                    if (RemoteToolKeywords.Any(k => lower.Contains(k)))
                    {
                        result.Add(displayName.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[RemoteAccessTools] Registry scan error: {ex.Message}");
            }
        }
    }
}

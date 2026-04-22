using System.Diagnostics;
using System.Runtime.InteropServices;
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
                var hwnd = process.MainWindowHandle;
                if (hwnd == IntPtr.Zero) return false;

                // Win32 visibility check beats MainWindowTitle heuristics.
                if (!IsWindowVisible(hwnd)) return false;

                var wp = new WINDOWPLACEMENT();
                wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                if (GetWindowPlacement(hwnd, ref wp))
                {
                    // Treat minimized/hidden as "not visible" to avoid tray/minimized false positives.
                    if (wp.showCmd == SW_HIDE || wp.showCmd == SW_SHOWMINIMIZED || wp.showCmd == SW_MINIMIZE)
                        return false;
                }

                // Extra guard: require non-empty title OR non-zero text length.
                if (!string.IsNullOrWhiteSpace(process.MainWindowTitle)) return true;
                return GetWindowTextLength(hwnd) > 0;
            }
            catch
            {
                return false;
            }
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_MINIMIZE = 6;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

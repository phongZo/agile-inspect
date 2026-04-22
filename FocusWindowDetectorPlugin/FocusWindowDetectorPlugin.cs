using AgileInspect.Code.PluginContracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FocusWindowDetectorPlugin.Code.Settings;
using Newtonsoft.Json.Linq;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;

namespace FocusWindowDetectorPlugin
{
    public class FocusWindowDetectorPlugin : IFocusWindowDetectorPlugin
    {
        public string pluginName => "FocusWindowDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        private IntPtr _hookHandle = IntPtr.Zero;
        private WinEventDelegate _winEventProc; 
        private IntPtr _lastWindowHandle = IntPtr.Zero;

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
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;

            PluginContext.Log(pluginName, "Start");
            PluginContext.Log(pluginName, $"triggerType: {triggerType}");

            if (triggerType.Equals("realtime", StringComparison.OrdinalIgnoreCase))
            {
                Stop(); 
                _winEventProc = new WinEventDelegate(WinEventProc);
                _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

                if (_hookHandle == IntPtr.Zero)
                {
                    PluginContext.Log(pluginName, "Failed to set WinEventHook.");
                }
                else
                {
                    CheckFocus(GetForegroundWindow());
                }
            }
            else
            {
                PluginContext.Log(pluginName, $"[FocusWindow] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _winEventProc = null;
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0) return; 
            CheckFocus(hwnd);
        }

        private void CheckFocus(IntPtr handle)
        {
            try
            {
                if (handle == IntPtr.Zero) return;
                if (handle == _lastWindowHandle) return; 

                _lastWindowHandle = handle;
                LogWindowInfo(handle);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error processing focus change: {ex.Message}");
            }
        }

        private static string GetAppName(uint pid)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                string? exePath = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return process.ProcessName;
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                return !string.IsNullOrWhiteSpace(vi.FileDescription) ? vi.FileDescription.Trim()
                    : !string.IsNullOrWhiteSpace(vi.ProductName) ? vi.ProductName.Trim()
                    : process.ProcessName;
            }
            catch
            {
                try { return Process.GetProcessById((int)pid).ProcessName; } catch { }
                return "Unknown";
            }
        }

        private void LogWindowInfo(IntPtr handle)
        {
            try
            {
                const int nChars = 256;
                StringBuilder buff = new StringBuilder(nChars);
                if (GetWindowText(handle, buff, nChars) > 0)
                {
                    string windowTitle = buff.ToString();
                    GetWindowThreadProcessId(handle, out uint pid);
                    string processName = "Unknown";
                    try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { }
                    string appName = GetAppName(pid);

                    var detectionResult = new JObject
                    {
                        ["processName"] = processName,
                        ["appName"] = appName,
                        ["windowTitle"] = windowTitle,
                        ["pid"] = pid,
                        ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    PluginContext.Log(pluginName, $"Focus: App='{appName}' Title='{windowTitle}' (PID: {pid})");
                    RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), detectionResult);
                    PluginContext.SendDetectionResult(pluginName, detectionResult);
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Failed to get window info: {ex.Message}");
            }
        }
    }
}

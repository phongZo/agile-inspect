using AgileInspect.Code.PluginContracts;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusWindowPlugin
{
    public class FocusWindowPlugin : IFocusWindowPlugin
    {
        public string Name => "FocusWindowPlugin";
        private AsyncTimerService _timerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        private IntPtr _lastWindowHandle = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public void Initialize()
        {
            PluginContext.Log(Name, "Initialize");
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
            PluginContext.Log(Name, "Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            double intervalMs = 500;
            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                intervalMs = interval * 1000;
            }
            else if (!triggerType.Equals("realtime", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"[FocusWindow] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }

            _timerService = new AsyncTimerService(intervalMs, CheckFocusAsync);
            _timerService.Start();
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _timerService?.Stop();
            _timerService?.Dispose();
            _timerService = null;
        }

        private async Task CheckFocusAsync()
        {
             try
            {
                var setting = StoreCfgJson.Instance.eventSetting;
                IntPtr handle = GetForegroundWindow();

                if (setting.triggerType.Equals("realtime", StringComparison.OrdinalIgnoreCase))
                {
                    if (handle != _lastWindowHandle && handle != IntPtr.Zero)
                    {
                        _lastWindowHandle = handle;
                        LogWindowInfo(handle);
                    }
                }
                else // interval
                {
                    if (handle != IntPtr.Zero)
                    {
                         LogWindowInfo(handle);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Error: {ex.Message}");
            }
            await Task.CompletedTask;
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
                    PluginContext.Log(Name, $"Focus: App='{processName}' Title='{windowTitle}' (PID: {pid})");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"Failed to get window info: {ex.Message}");
            }
        }
    }
}

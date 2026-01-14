using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using AgileInspect.Code.PluginContracts;
using Newtonsoft.Json;
using PasteDetectorPlugin.Code.Settings;

namespace PasteDetectorPlugin.Code
{
    public class PasteDetector
    {
        private NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private AutomationEventHandler _automationHandler;
        private StoreCfgJson _config;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public PasteDetector(StoreCfgJson config)
        {
            _config = config;
            _proc = HookCallback;
        }

        public void Start()
        {
            try 
            {
                _hookID = SetHook(_proc);
                PluginContext.Log("PasteDetectorPlugin", "Keyboard Hook started.");

                _automationHandler = new AutomationEventHandler(OnUIAutomationEvent);
                Automation.AddAutomationEventHandler(
                    InvokePattern.InvokedEvent, 
                    AutomationElement.RootElement, 
                    TreeScope.Descendants, 
                    _automationHandler);
                
                PluginContext.Log("PasteDetectorPlugin", "UI Automation Listener started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log("PasteDetectorPlugin", $"Failed to start: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_hookID != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }

                if (_automationHandler != null)
                {
                    Automation.RemoveAutomationEventHandler(
                        InvokePattern.InvokedEvent, 
                        AutomationElement.RootElement, 
                        _automationHandler);
                    _automationHandler = null;
                }
                
                PluginContext.Log("PasteDetectorPlugin", "Stopped.");
            }
            catch {}
        }

        private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool ctrl = (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool shift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                if (ctrl && vkCode == NativeMethods.VK_V)
                {
                    LogPasteEvent("Keyboard (Ctrl+V)");
                }
                else if (shift && vkCode == NativeMethods.VK_INSERT)
                {
                    LogPasteEvent("Keyboard (Shift+Insert)");
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void OnUIAutomationEvent(object sender, AutomationEventArgs e)
        {
            try
            {
                var element = sender as AutomationElement;
                if (element == null) return;

                string name = element.Current.Name;
                if (string.IsNullOrEmpty(name)) return;

                string lowerName = name.ToLower();
                if (lowerName == "paste" || lowerName == "dán" || lowerName == "paste options" || lowerName == "keep text only")
                {
                    LogPasteEvent($"Mouse/Menu ({name})");
                }
            }
            catch {}
        }

        private void LogPasteEvent(string source)
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                string windowTitle = "Unknown";
                string processName = "Unknown";
                uint pid = 0;

                if (hwnd != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(256);
                    if (GetWindowText(hwnd, sb, 256) > 0) windowTitle = sb.ToString();

                    GetWindowThreadProcessId(hwnd, out pid);
                    try { processName = Process.GetProcessById((int)pid).ProcessName; } catch {}
                }

                string msg = $"Paste Detected: Source={source}, App='{processName}', Title='{windowTitle}' (PID: {pid})";
                PluginContext.Log("PasteDetectorPlugin", msg);

                var result = new
                {
                    eventType = "paste_detect",
                    source = source,
                    app = processName,
                    title = windowTitle,
                    pid = pid,
                    timestamp = DateTime.Now
                };
                PluginContext.SendDetectionResult("PasteDetectorPlugin", JsonConvert.SerializeObject(result));
            }
            catch {}
        }
    }
}

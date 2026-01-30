using AgileInspect.Code.PluginContracts;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using PrintScreenDetectorPlugin.Code.Settings;
using Newtonsoft.Json.Linq;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;

namespace PrintScreenDetectorPlugin
{
    public class PrintScreenDetectorPlugin : IPrintScreenDetectorPlugin
    {
        public string pluginName => "PrintScreenDetectorPlugin";
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_SNAPSHOT = 0x2C;

        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

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

                _proc = HookCallback;
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                }

                if (_hookHandle == IntPtr.Zero)
                {
                    PluginContext.Log(pluginName, "Failed to set Keyboard Hook.");
                }
            }
            else
            {
                PluginContext.Log(pluginName, $"[PrintScreen] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                PluginContext.Log(pluginName, "Keyboard Hook uninstalled.");
            }
            _proc = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_SNAPSHOT)
                {
                    Task.Run(() => HandlePrintScreen());
                }
            }
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private void HandlePrintScreen()
        {
             try
            {
                PluginContext.Log(pluginName, "PrintScreen pressed");
                var result = new JObject{
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), result);
                PluginContext.SendDetectionResult(pluginName, result);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Error: {ex.Message}");
            }
        }
    }
}

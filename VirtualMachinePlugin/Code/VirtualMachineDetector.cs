using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using System.Diagnostics;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace VirtualMachinePlugin
{
    public class VirtualMachineDetector
    {
        #region Singleton
        public static VirtualMachineDetector Instance { get; set; }
        public VirtualMachineDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "VirtualMachinePlugin";

        private static readonly string[] VmKeywords =
        [
            "virtualbox",
            "vmware",
            "qemu",
            "kvm",
            "xen",
            "hyper-v",
            "hyperv",
            "parallels",
            "virtual machine"
        ];

        private static readonly string[] VmProcessKeywords =
        [
            "vmware",
            "vmplayer",
            "vmware-vmx",
            "vmware-hostd",
            "vmware-authd",
            "virtualbox",
            "virtualboxvm",
            "vboxheadless",
            "vboxsvc",
            "qemu",
            "qemu-system",
            "prl_cc",
            "prl_vm_app",
            "vmcompute",
            "vmwp"
        ];

        public void Check()
        {
            var vmHints = GetVirtualizationHints();
            bool isVirtualMachine = vmHints.Count > 0;

            PluginContext.Log(pluginName, $"[VirtualMachine] Running in VM: {isVirtualMachine}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = isVirtualMachine,
                ["appList"] = JToken.FromObject(vmHints)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> GetVirtualizationHints()
        {
            var hints = new List<string>();
            hints.AddRange(GetVmGuestHints());
            hints.AddRange(GetVmRuntimeHints());

            return hints.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        }

        private List<string> GetVmGuestHints()
        {
            var hints = new List<string>();
            var guestKeywords = GetConfiguredGuestKeywords();
            try
            {
                using var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey == null) return hints;

                var values = new[]
                {
                    biosKey.GetValue("SystemManufacturer")?.ToString(),
                    biosKey.GetValue("SystemProductName")?.ToString(),
                    biosKey.GetValue("BIOSVendor")?.ToString(),
                    biosKey.GetValue("BaseBoardManufacturer")?.ToString(),
                    biosKey.GetValue("BaseBoardProduct")?.ToString()
                }.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                foreach (var value in values)
                {
                    var lower = value!.ToLowerInvariant();
                    if (guestKeywords.Any(k => lower.Contains(k)))
                    {
                        hints.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[VirtualMachine] Detection error: {ex.Message}");
            }
            return hints;
        }

        private List<string> GetVmRuntimeHints()
        {
            var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processKeywords = GetConfiguredProcessKeywords();
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    var name = Normalize(process.ProcessName);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    foreach (var keyword in processKeywords)
                    {
                        if (name.Contains(keyword))
                        {
                            hints.Add(process.ProcessName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[VirtualMachine] Runtime process scan error: {ex.Message}");
            }

            return hints.ToList();
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private string[] GetConfiguredGuestKeywords()
        {
            var configured = StoreCfgJson.Instance?.eventSetting?.eventParams?.keywords;
            if (configured == null || configured.Length == 0)
            {
                return VmKeywords;
            }

            return configured
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .ToArray();
        }

        private string[] GetConfiguredProcessKeywords()
        {
            var configured = StoreCfgJson.Instance?.eventSetting?.eventParams?.processes;
            if (configured == null || configured.Length == 0)
            {
                return VmProcessKeywords;
            }

            return configured
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .ToArray();
        }
    }
}

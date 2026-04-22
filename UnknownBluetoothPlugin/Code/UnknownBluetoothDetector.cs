using System.Diagnostics;
using System.Text.RegularExpressions;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace UnknownBluetoothPlugin
{
    public class UnknownBluetoothDetector
    {
        #region Singleton
        public static UnknownBluetoothDetector Instance { get; set; }
        public UnknownBluetoothDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "UnknownBluetoothPlugin";

        public void Check()
        {
            var unknownDevicesDetailed = GetUnknownBluetoothDevicesDetailed();
            var unknownNames = unknownDevicesDetailed
                .Select(d => (string?)d["FriendlyName"])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            bool detected = unknownNames.Count > 0;

            PluginContext.Log(pluginName, $"[UnknownBluetooth] Unknown bluetooth paired: {detected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                // Back-compat: keep the original string list
                ["deviceList"] = JToken.FromObject(unknownNames),
                // New: include type/category when available
                ["deviceListDetailed"] = JToken.FromObject(unknownDevicesDetailed)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<JObject> GetUnknownBluetoothDevicesDetailed()
        {
            var result = new List<JObject>();
            try
            {
                // Collect FriendlyName + InstanceId, and (best effort) Bluetooth ClassOfDevice.
                // ClassOfDevice is a 24-bit int; bits 8-12 represent Major Device Class.
                var output = RunPowerShell(
                    "$ErrorActionPreference='SilentlyContinue';" +
                    "$devs = Get-PnpDevice -Class Bluetooth -Status OK | Select-Object FriendlyName, InstanceId;" +
                    "$devs | ForEach-Object {" +
                    "  $cod=$null;" +
                    "  try { $p=Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Bluetooth_ClassOfDevice' -ErrorAction Stop; $cod=$p.Data } catch {}" +
                    "  [pscustomobject]@{ FriendlyName=$_.FriendlyName; InstanceId=$_.InstanceId; ClassOfDevice=$cod }" +
                    "} | ConvertTo-Json -Compress"
                );
                if (string.IsNullOrWhiteSpace(output)) return result.ToList();

                var token = JToken.Parse(output.Trim());
                var arr = token.Type == JTokenType.Array ? (JArray)token : new JArray(token);

                foreach (var t in arr)
                {
                    var name = (string?)t["FriendlyName"] ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var lower = name.ToLowerInvariant();
                    if (lower.Contains("unknown") || lower.Contains("bluetooth device") || Regex.IsMatch(lower, @"^ble\s"))
                    {
                        int? cod = null;
                        try
                        {
                            var codTok = t["ClassOfDevice"];
                            if (codTok != null && codTok.Type != JTokenType.Null)
                                cod = codTok.Value<int?>();
                        }
                        catch { /* ignore */ }

                        var major = cod.HasValue ? (int?)((cod.Value >> 8) & 0x1F) : null;
                        var majorName = major.HasValue ? BluetoothMajorClassName(major.Value) : null;

                        result.Add(new JObject
                        {
                            ["FriendlyName"] = name,
                            ["InstanceId"] = (string?)t["InstanceId"],
                            ["ClassOfDevice"] = cod.HasValue ? cod.Value : new JValue((object?)null),
                            ["MajorClass"] = major.HasValue ? major.Value : new JValue((object?)null),
                            ["MajorClassName"] = majorName != null ? majorName : new JValue((object?)null)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[UnknownBluetooth] Detection error: {ex.Message}");
            }

            return result
                .OrderBy(x => (string?)x["FriendlyName"] ?? "")
                .ToList();
        }

        private static string BluetoothMajorClassName(int major)
        {
            // Bluetooth Class of Device (CoD) Major Device Class values.
            return major switch
            {
                0x00 => "Misc",
                0x01 => "Computer",
                0x02 => "Phone",
                0x03 => "LAN/Network Access",
                0x04 => "Audio/Video",
                0x05 => "Peripheral",
                0x06 => "Imaging",
                0x07 => "Wearable",
                0x08 => "Toy",
                0x09 => "Health",
                0x1F => "Uncategorized",
                _ => "Unknown"
            };
        }

        private string RunPowerShell(string command)
        {
            using var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
    }
}

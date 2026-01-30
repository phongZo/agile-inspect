using System.Runtime.InteropServices;
using AgileInspect.Code.PluginContracts;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using AntivirusDetectorPlugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wscapi.Interop; // Add COM reference to wscapi.dll

namespace WatermarkDetectorPlugin
{
    class AntivirusInfo
    {
        public string name { get; set; }
        public string version { get; set; } = "";
        public string signatureVersion { get; set; } = "";
        public string signatureLastUpdated { get; set; } = "";
        public string state { get; set; }
        public bool localUpToDate { get; set; }
        public string stateTimestamp { get; set; }
        public int signatureAge { get; set; }
    }
    [Flags]
    public enum WSC_SECURITY_PROVIDER
    {
        /// <summary>Represents no provider.</summary>
        NONE = 0,

        /// <summary>Represents the aggregation of all firewalls for this computer.</summary>
        FIREWALL = 0x1,

        /// <summary>Represents the Automatic updating settings for this computer.</summary>
        AUTOUPDATE_SETTINGS = 0x2,

        /// <summary>Represents the aggregation of all antivirus products for this computer.</summary>
        ANTIVIRUS = 0x4,

        /// <summary>Represents the aggregation of all antispyware products for this computer.</summary>
        ANTISPYWARE = 0x8,

        /// <summary>Represents the settings that restrict the access of web sites in each of the internet zones.</summary>
        INTERNET_SETTINGS = 0x10,

        /// <summary>Represents the User Account Control settings on this machine.</summary>
        USER_ACCOUNT_CONTROL = 0x20,

        /// <summary>Represents the running state of the Security Center service on this machine.</summary>
        SERVICE = 0x40,

        /// <summary>Aggregates all of the items that Security Center monitors.</summary>
        ALL = FIREWALL |
              AUTOUPDATE_SETTINGS |
              ANTIVIRUS |
              ANTISPYWARE |
              INTERNET_SETTINGS |
              USER_ACCOUNT_CONTROL |
              SERVICE
    }
    public class AntivirusDetectorPlugin : IAntivirusDetectorPlugin
    {
        private AsyncTimerService _antivirusTimerService;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public string pluginName => "AntivirusDetectorPlugin";

        public void Initialize()
        {
            PluginContext.Log(pluginName, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
                : null;

            setting.eventParams = parsedEventParams ?? setting.eventParams;
            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            setting.triggerParams = parsedTriggerParams ?? setting.triggerParams;

            StoreCfgJson.Instance.eventSetting = setting;

            PluginContext.Log(pluginName, $"Parameters is set ");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var interval = setting.triggerParams.interval;

            PluginContext.Log(pluginName, "Start");
            PluginContext.Log(pluginName, $"triggerType: {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _antivirusTimerService = new AsyncTimerService(interval * 1000, CheckAntivirusTimerCallbackAsync);
                _antivirusTimerService.Start();
            }
            else
            {
                PluginContext.Log(pluginName, $"[AntivirusDetector] Unsupported triggerType '{triggerType}', plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(pluginName, "Stopped.");
            _antivirusTimerService?.Stop();
            _antivirusTimerService?.Dispose();
            _antivirusTimerService = null;
        }

        private async Task CheckAntivirusTimerCallbackAsync()
        {
            PluginContext.Log(pluginName, "[AntivirusDetector] interval hit");
            await Task.Run(() =>
            {
                var appList = new List<AntivirusInfo>();

                // Query Antivirus products
                appList.AddRange(GetSecurityProducts(WSC_SECURITY_PROVIDER.ANTIVIRUS));

                // Query Antispyware products if needed
                // appList.AddRange(GetSecurityProducts(WSC_SECURITY_PROVIDER.WSC_SECURITY_PROVIDER_ANTISPYWARE));

                var output =  new JObject
                {
                    ["appList"] = JToken.FromObject(appList)
                };
                string json = JsonConvert.SerializeObject(output);
                PluginContext.Log(pluginName, $"[AntivirusDetector] Current antivirus: {output}");
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), output);
                PluginContext.SendDetectionResult(pluginName, output);
            });
        }
        static List<AntivirusInfo> GetSecurityProducts(WSC_SECURITY_PROVIDER provider)
        {
            var list = new List<AntivirusInfo>();

            IWSCProductList productList = new WSCProductList();
            productList.Initialize((uint)provider);

            int count = productList.Count;
            for (uint i = 0; i < count; i++)
            {
                IWscProduct product = productList.Item[i];

                string name = product.ProductName;
                string productStateStr = DecodeProductState(product.ProductState);
                bool productStatusStr = (product.SignatureStatus == _WSC_SECURITY_SIGNATURE_STATUS.WSC_SECURITY_PRODUCT_UP_TO_DATE);
                string productStateTimestamp = "";
                try
                {
                    var rawTs = product.ProductStateTimestamp;  // raw BSTR
                    productStateTimestamp = rawTs;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse timestamp for {name}: {ex.Message}");
                }
                string appVersion = "";
                string sigVersion = "";
                string sigLastUpdated = "";
                int signatureAge = 0;
                // Defender special handling
                if (name.IndexOf("defender", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        using (var searcher = new System.Management.ManagementObjectSearcher(
                            @"root\Microsoft\Windows\Defender",
                            "SELECT AMProductVersion,AntivirusSignatureVersion,AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus"))
                        {
                            foreach (System.Management.ManagementObject def in searcher.Get())
                            {
                                appVersion = def["AMProductVersion"]?.ToString() ?? "";
                                sigVersion = def["AntivirusSignatureVersion"]?.ToString() ?? "";
                                var sigAge = def["AntivirusSignatureAge"]?.ToString() ?? "0";

                                if (def["AntivirusSignatureLastUpdated"] != null)
                                {
                                    DateTime dt = System.Management.ManagementDateTimeConverter
                                        .ToDateTime(def["AntivirusSignatureLastUpdated"].ToString());
                                    sigLastUpdated = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                signatureAge = int.Parse(sigAge);
                            }
                        }
                    }
                    catch { 
                    
                    }
                }
                else
                {
                    // Try executable file version from RemediationPath
                    try
                    {
                        var exePath = product.RemediationPath;
                        if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                        {
                            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                            appVersion = versionInfo.ProductVersion;
                        }
                    }
                    catch { }
                }

                list.Add(new AntivirusInfo
                {
                    name = name,
                    version = appVersion,
                    state = productStateStr,
                    localUpToDate = productStatusStr,
                    stateTimestamp = productStateTimestamp,
                    signatureVersion = sigVersion,
                    signatureLastUpdated = sigLastUpdated,
                    signatureAge = signatureAge,
                });

                Marshal.ReleaseComObject(product);
            }

            Marshal.ReleaseComObject(productList);
            return list;
        }

        static string DecodeProductState(WSC_SECURITY_PRODUCT_STATE state)
        {
            switch (state)
            {
                case WSC_SECURITY_PRODUCT_STATE.WSC_SECURITY_PRODUCT_STATE_ON:
                    return "on";
                case WSC_SECURITY_PRODUCT_STATE.WSC_SECURITY_PRODUCT_STATE_OFF:
                    return "off";
                case WSC_SECURITY_PRODUCT_STATE.WSC_SECURITY_PRODUCT_STATE_SNOOZED:
                    return "snoozed";
                case WSC_SECURITY_PRODUCT_STATE.WSC_SECURITY_PRODUCT_STATE_EXPIRED:
                    return "expired";
                default:
                    return "unknown";
            }
        }

    }
}


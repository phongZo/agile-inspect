using AgileInspect.Code;
using AgileInspect.Code.Settings;
using AgileInspect.Code.Settings.Response;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AgileInspect
{
    public class HashCheckInterval
    {
        private static HashCheckInterval _instance;
        public static HashCheckInterval Instance => _instance ??= new HashCheckInterval();
        public PluginManager PluginManager { get; private set; } = new PluginManager();

        private Timer _timer;
        private int _intervalMs = 3000; // default 3 seconds
        private bool _isRunning;
        private static readonly HttpClient client = new HttpClient();
        public bool IsConfigUpdated = false;
        private HashCheckInterval() { }

        public void Start(bool skipImmediate = false)
        {
            DebugLog.WriteLine("[HashCheckInterval] Start HashCheckInterval");
            if (_isRunning) return;

            _intervalMs = StoreCfgJson.Instance.eventConfig.settingPullInterval > 0
                ? StoreCfgJson.Instance.eventConfig.settingPullInterval
                : _intervalMs;

            var delay = skipImmediate ? _intervalMs : 0;

            _timer = new Timer(async _ => await GetHash(), null, delay, _intervalMs);
            _isRunning = true;
        }

        public void Stop()
        {
            _timer?.Dispose();
            _isRunning = false;
        }

        public void UpdateInterval(int milliseconds)
        {
            _intervalMs = milliseconds;
            if (_isRunning)
            {
                _timer?.Change(0, _intervalMs); // restart with new interval
            }
        }

        public async Task GetHash()
        {
            try
            {
                var query = "customerId=" + StoreCfgJson.Instance.customerID + "&clientName=" + MachineName.Instance.Name + "&os=windows" + "&version=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var url = StoreCfgJson.Instance.serverUrl + "/client/my-hash?" + query;
                HttpResponseMessage response = await client.GetAsync(url);
                string data = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    HashData responseData = new HashData();
                    try
                    {
                        var jsonSerializerSettings = new JsonSerializerSettings()
                        {
                            Error = (sender, errorEventArgs) =>
                            {
                                //You can use your "jsonString" here
                                var error = errorEventArgs;
                                Console.WriteLine(error);
                            },
                        };
                        responseData = JsonConvert.DeserializeObject<HashData>(data, jsonSerializerSettings);

                        if (responseData.data != null)
                        {
                            var serverHash = responseData.data.hash;
                            var currentHash = StoreCfgJson.Instance.hash;

                            if (serverHash != currentHash)
                            {
                                DebugLog.WriteLine($"[GetHash] Detected new hash from server: {serverHash}. Current hash: {currentHash}");

                                StoreCfgJson.Instance.hash = serverHash;
                                StoreCfgLoader.Save();//save hash

                                await GetSetting();
                            }
                            else
                            {
                                DebugLog.WriteLine($"[GetHash] Hash unchanged. Skipping GetSetting(). Hash: {serverHash}");
                            }
                        }
                        else
                        {
                            DebugLog.WriteLine("[GetHash] Response data is null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteLine("\nFAILED TO DESERIALIZE JSON in GetHash");
                        DebugLog.WriteLine(ex.Message);
                    }
                }
                else
                {
                    DebugLog.WriteLine($"[GetHash] Error: {response.StatusCode}, {data}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[GetHash] API error: {ex.Message}");
            }
        }

        private async Task GetSetting()
        {
            try
            {
                var query = "customerId=" + StoreCfgJson.Instance.customerID + "&clientName=" + MachineName.Instance.Name + "&os=windows" + "&version=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var url = StoreCfgJson.Instance.serverUrl + "/client/my-settings?" + query;
                HttpResponseMessage response = await client.GetAsync(url);
                string data = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    SettingData responseData = new SettingData();
                    try
                    {
                        var jsonSerializerSettings = new JsonSerializerSettings()
                        {
                            Error = (sender, errorEventArgs) =>
                            {
                                //You can use your "jsonString" here
                                var error = errorEventArgs;
                                Console.WriteLine(error);
                            },
                        };
                        responseData = JsonConvert.DeserializeObject<SettingData>(data, jsonSerializerSettings);

                        var serverEventConfig = responseData?.data;
                        var currentEventConfig = StoreCfgJson.Instance.eventConfig;

                        if (serverEventConfig != null)
                        {
                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] Detected new config hash, applying update...");

                            currentEventConfig.eventSettings = [.. serverEventConfig.eventSettings];
                            currentEventConfig.settingPullInterval = serverEventConfig.settingPullInterval;
                            currentEventConfig.settingHash = serverEventConfig.settingHash;

                            CleanUpEventSetting(currentEventConfig);
                            StoreCfgLoader.Save();
                            StoreCfgLoader.Load();

                            PluginManager.Instance.LoadPlugins();
                            PluginManager.Instance.StopAll();
                            PluginManager.Instance.StartAll();
                            IsConfigUpdated = true;

                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] EventConfig updated from server. Restarting required plugins.");
                        }
                        else
                        {
                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] Server event config is null. Skipped.");
                        }

                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteLine("\nFAILED TO DESERIALIZE JSON in GetURLContents");
                        DebugLog.WriteLine(ex.Message);
                    }
                }

            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"API error: {ex.Message}");
            }
        }
        public static void CleanUpEventSetting(EventConfig config)
        {
            if (config?.eventSettings == null) return;

            foreach (var setting in config.eventSettings)
            {
                CleanObject(setting.eventParams);
                CleanObject(setting.triggerParams);
            }
        }

        private static void CleanObject(object obj)
        {
            if (obj == null) return;

            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                var value = prop.GetValue(obj);

                if (value is System.Collections.ICollection collection && collection.Count == 0)
                {
                    prop.SetValue(obj, null);
                    continue;
                }

                if (prop.PropertyType == typeof(string) && string.IsNullOrEmpty((string)value))
                {
                    prop.SetValue(obj, null);
                    continue;
                }

                if (prop.PropertyType.IsValueType)
                {
                    continue;
                }

                if (prop.PropertyType.IsClass)
                {
                    CleanObject(value);
                }
            }
        }
    }

}

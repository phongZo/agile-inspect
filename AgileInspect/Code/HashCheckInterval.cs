using AgileInspect.Code;
using AgileInspect.Code.Settings;
using AgileInspect.Code.Settings.Web;
using AgileInspect.Code.Settings.Web.Response;
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
        #region Singleton

        private static HashCheckInterval _instance;
        public static HashCheckInterval Instance => _instance ??= new HashCheckInterval();
        private HashCheckInterval() { }
        #endregion

        private Timer _timer;
        private int _intervalMs = 3000; // default 3 seconds
        private bool _isRunning;
        public bool IsConfigUpdated = false;
        private string _serverHash = null;

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
                var query = "customerId=" + StoreCfgJson.Instance.customerID +
                    "&clientName=" + MachineName.Instance.Name +
                    "&os=windows" +
                    "&version=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

                var url = StoreCfgJson.Instance.serverUrl + "/client/my-hash?" + query;
                DebugLog.WriteLine($"[GetHash] Getting HASH from server ...");
                var response = await SignedHttpClient.Instance.SendSignedRequestAsync(HttpMethod.Get, url);
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
                            _serverHash = responseData.data.hash;
                            var currentHash = StoreCfgJson.Instance.hash;

                            if (_serverHash != currentHash)
                            {
                                DebugLog.WriteLine($"[GetHash] Detected new hash from server: {_serverHash}. Current hash: {currentHash}");

                                await GetSetting();
                            }
                            else
                            {
                                DebugLog.WriteLine($"[GetHash] Hash unchanged. Skipping GetSetting(). Hash: {_serverHash}");
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
                DebugLog.WriteLine($"[HashCheckInterval] [GetSetting] Getting SETTING from server ... ");
                var response = await SignedHttpClient.Instance.SendSignedRequestAsync(HttpMethod.Get, url);
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
                            // GET current config
                            var currentConfig = StoreCfgLoader.Instance.Get();

                            currentConfig.hash = _serverHash;

                            currentEventConfig.eventSettings = [.. serverEventConfig.eventSettings];
                            currentEventConfig.settingPullInterval = serverEventConfig.settingPullInterval;
                            currentEventConfig.settingHash = serverEventConfig.settingHash;

                            CleanUpEventSetting(currentEventConfig);
                            currentConfig.eventConfig = currentEventConfig;

                            // SAVE to file and set current config again
                            StoreCfgLoader.Instance.Save(currentConfig);

                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] EventConfig updated from server. Restarting required plugins.");

                            PluginManager.Instance.StopAll();
                            PluginManager.Instance.LoadPlugins();
                            PluginManager.Instance.StartAll();
                            IsConfigUpdated = true;
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
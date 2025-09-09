using AgileInspect.Code;
using AgileInspect.Code.Settings;
using AgileInspect.Code.Settings.Web;
using AgileInspect.Code.Settings.Web.Response;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        public bool IsConfigUpdated = false;
        private string _serverHash = null;
        private HashCheckInterval() { }

        public void Start(bool skipImmediate = false)
        {
            DebugLog.WriteLine("[HashCheckInterval] Start HashCheckInterval");
            if (_isRunning) return;

            _intervalMs = StoreCfgJson.Instance.settingPullInterval > 0
                ? StoreCfgJson.Instance.settingPullInterval
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
                            var currentHash = StoreCfgJson.Instance.settingHash;

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

                        var serverEventSettings = responseData?.data;
                        var currentEventSettings = StoreCfgJson.Instance.eventSettings;

                        if (currentEventSettings != null)
                        {
                            var currentConfig = StoreCfgLoader.Instance.Get();

                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] Detected new config hash, applying update...");

                            currentEventSettings = [.. serverEventSettings.eventSettings];

                            CleanUpEventSetting(currentEventSettings);
                            currentConfig.eventSettings = currentEventSettings;
                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] EventConfig updated from server. Restarting required plugins.");

                            // SAVE to file and set current config again
                            StoreCfgLoader.Instance.Save(currentConfig);

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
        public static void CleanUpEventSetting(List<EventSetting> eventSettings)
        {
            if (eventSettings == null) return;

            foreach (var setting in eventSettings)
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

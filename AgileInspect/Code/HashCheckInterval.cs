using AgileInspect.Code.Settings;
using AgileInspect.Code.Settings.Response;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AgileInspect
{
    public class HashCheckInterval
    {
        private static HashCheckInterval _instance;
        public static HashCheckInterval Instance => _instance ??= new HashCheckInterval();

        private Timer _timer;
        private int _intervalMs = 3000; // default 3 seconds
        private bool _isRunning;

        private HashCheckInterval() { }

        public void Start()
        {
            DebugLog.WriteLine("[HashCheckInterval] Start HashCheckInterval");
            if (_isRunning) return;

            // get in config
            _intervalMs = StoreCfgJson.Instance.EventConfig.SettingPullInterval > 0
                    ? StoreCfgJson.Instance.EventConfig.SettingPullInterval
                    : _intervalMs; _timer = new Timer(async _ => await GetHash(), null, 0, _intervalMs);

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

        private async Task GetHash()
        {
            try
            {
                using var client = new HttpClient();
                var query = "customerId=" + StoreCfgJson.Instance.CustomerID + "&clientName=" + MachineName.Instance.Name + "&os=windows" + "&version=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var url = StoreCfgJson.Instance.ServerUrl + "/client/my-hash?" + query;
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
                        if (responseData.data != null && responseData.data.hash != StoreCfgJson.Instance.Hash)
                        {
                            await GetSetting(responseData.data.hash);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteLine("\nFAILED TO DESERIALIZE JSON in GetURLContents");
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
                DebugLog.WriteLine($"API error: {ex.Message}");
            }
        }
        private async Task GetSetting(string hash)
        {
            try
            {
                using var client = new HttpClient();
                var query = "customerId=" + StoreCfgJson.Instance.CustomerID + "&clientName=" + MachineName.Instance.Name + "&os=windows" + "&version=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var url = StoreCfgJson.Instance.ServerUrl + "/client/my-settings?" + query;
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
                        var currentEventConfig = StoreCfgJson.Instance.EventConfig;

                        if (serverEventConfig != null && serverEventConfig.settingHash != currentEventConfig.SettingHash)
                        {
                            currentEventConfig.EventSettings = serverEventConfig.eventSettings.ToList();
                            currentEventConfig.SettingPullInterval = serverEventConfig.settingPullInterval;
                            currentEventConfig.SettingHash = serverEventConfig.settingHash;

                            StoreCfgLoader.Save();
                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] UPDATE new EventConfig from server.");
                        }
                        else
                        {
                            DebugLog.WriteLine("[HashCheckInterval] [GetSetting] settingHash unchanged, bypass update EventConfig");
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
    }

}

using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AgileInspect.Code
{
    public class EventQueueService
    {
        #region Singleton
        public static EventQueueService Instance { get; private set; }

        public EventQueueService()
        {
            Instance = this;
            _filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AgileInspect",
                "event_queue.json"
            );

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, "[]");
            }
        }
        #endregion

        private readonly object _lock = new();
        string _filePath;
        public void Enqueue(string pluginName, JToken json)
        {
            var saveEvent = new SaveEvent
            {
                eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName),
                clientName = MachineName.Instance.Name,
                customerId = StoreCfgJson.Instance.customerID,
                data = json
            };

            var serialized = JsonConvert.SerializeObject(saveEvent);
            EventLog.WriteLine(serialized);

            lock (_lock)
            {
                List<JToken> queue;

                try
                {
                    var content = File.ReadAllText(_filePath);
                    queue = JsonConvert.DeserializeObject<List<JToken>>(content) ?? new List<JToken>();
                }
                catch
                {
                    queue = new List<JToken>();
                }

                var doc = JToken.Parse(serialized);
                queue.Add(doc);

                var updatedContent = JsonConvert.SerializeObject(queue);
                File.WriteAllText(_filePath, updatedContent);

                DebugLog.WriteLine($"[EventQueueService] [Enqueue] Event added to queue. Total: {queue.Count}");
            }

        }

        public async Task SendQueueAsync()
        {
            List<JToken> originalQueue;

            lock (_lock)
            {
                if (!File.Exists(_filePath))
                {
                    DebugLog.WriteLine("[SendQueueAsync] Queue file does not exist.");
                    return;
                }

                var content = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    //DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
                    return;
                }

                originalQueue = JsonConvert.DeserializeObject<List<JToken>>(content) ?? [];
            }

            if (originalQueue.Count == 0)
            {
                //DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
                return;
            }

            var newQueue = new List<JToken>();

            foreach (var item in originalQueue)
            {
                var json = JsonConvert.SerializeObject(item);
                DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Sending JSON: {json}");

                bool success = await TrySendToServerAsync(json);
                if (success)
                {
                    DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Send success.");
                }
                else
                {
                    DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Send failed. Will retry next time.");
                    newQueue.Add(item); // Keep to retry at next interval hit
                    await Task.Delay(10000); // wait 10 seconds
                }
            }

            lock (_lock)
            {
                var updatedContent = JsonConvert.SerializeObject(newQueue);
                File.WriteAllText(_filePath, updatedContent);
                DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Updated queue. Remaining: {newQueue.Count}");
            }
        }

        private async Task<bool> TrySendToServerAsync(string json)
        {
            try
            {
                using var client = new HttpClient();
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send POST request
                var response = await client.PostAsync(StoreCfgJson.Instance.serverUrl + "/event-log/create", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    DebugLog.WriteLine("[EventQueueService] [TrySendToServerAsync] Success: " + responseString);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    DebugLog.WriteLine($"[EventQueueService] [TrySendToServerAsync] Error: {response.StatusCode}, {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[EventQueueService] [TrySendToServerAsync] Exception: {ex.Message}");
                return false;
            }
        }

    }
}

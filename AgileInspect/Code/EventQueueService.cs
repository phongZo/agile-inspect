using AgileInspect.Code.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        public void Enqueue(string pluginName, Dictionary<string, JsonElement> json)
        {

            var saveEvent = new SaveEvent
            {
                eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName),
                clientName = MachineName.Instance.Name,
                customerId = StoreCfgJson.Instance.customerID,
                data = json
            };

            var serialized = JsonSerializer.Serialize(saveEvent, new JsonSerializerOptions { WriteIndented = true });
            EventLog.WriteLine(serialized);

            lock (_lock)
            {
                List<JsonElement> queue;

                try
                {
                    var content = File.ReadAllText(_filePath);
                    queue = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new List<JsonElement>();
                }
                catch
                {
                    queue = new List<JsonElement>();
                }

                using var doc = JsonDocument.Parse(serialized);
                queue.Add(doc.RootElement.Clone());

                var updatedContent = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, updatedContent);

                DebugLog.WriteLine($"[EventQueueService] [Enqueue] Event added to queue. Total: {queue.Count}");
            }

        }

        public async Task SendQueueAsync()
        {
            List<JsonElement> originalQueue;

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

                originalQueue = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? [];
            }

            if (originalQueue.Count == 0)
            {
                //DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
                return;
            }

            var newQueue = new List<JsonElement>();

            foreach (var item in originalQueue)
            {
                var json = item.GetRawText();
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
                }
            }

            lock (_lock)
            {
                var updatedContent = JsonSerializer.Serialize(newQueue, new JsonSerializerOptions { WriteIndented = true });
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

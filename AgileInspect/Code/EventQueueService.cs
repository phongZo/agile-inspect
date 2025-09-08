using AgileInspect.Code.Settings;
using AgileInspect.Code.Settings.Web;
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
        public static readonly EventQueueService Instance = new EventQueueService();

        private EventQueueService()
        {
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
        private readonly string _filePath;

        public void Enqueue(string pluginName, Dictionary<string, JsonElement> json)
        {
            var saveEvent = new SaveEvent
            {
                eventType = StoreCfgLoader.Instance.MapPluginNameToEventType(pluginName),
                clientName = MachineName.Instance.Name,
                customerId = StoreCfgJson.Instance.customerID,
                data = json,
                createdAt = DateTime.UtcNow
            };

            var serialized = JsonSerializer.Serialize(saveEvent);
            EventLog.Write(serialized);

            lock (_lock)
            {
                List<JsonElement> queue;

                try
                {
                    var content = File.ReadAllText(_filePath);
                    queue = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new();
                }
                catch
                {
                    queue = new();
                }

                using var doc = JsonDocument.Parse(serialized);
                queue.Add(doc.RootElement.Clone());

                var updatedContent = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
                SafeWriteToFile(_filePath, updatedContent);

                DebugLog.WriteLine($"[EventQueueService] [Enqueue] Event added to queue. Total: {queue.Count}");
            }
        }

        public async Task SendQueueAsync()
        {
            List<JsonElement> queue;

            lock (_lock)
            {
                if (!File.Exists(_filePath)) return;
                var content = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(content)) return;

                queue = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new();
            }

            if (queue.Count == 0) return;

            for (int i = 0; i < queue.Count; i++)
            {
                var item = queue[i];
                string json = item.GetRawText();

                DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Try to send to server...");

                bool success = await TrySendToServerAsync(json);

                // save file when send success 1 queue
                if (success)
                {
                    DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Send success.");

                    lock (_lock)
                    {
                        queue.RemoveAt(i);
                        i--;

                        var updatedContent = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
                        SafeWriteToFile(_filePath, updatedContent);
                        DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Queue updated. Remaining: {queue.Count}");
                    }
                }
                else
                {
                    DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Send failed. Keeping item in queue. Remaining: {queue.Count}");
                }
            }
        }

        private async Task<bool> TrySendToServerAsync(string json)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = StoreCfgJson.Instance.serverUrl + "/event-log/create";

                var response = await SignedHttpClient.Instance.SendSignedRequestAsync(HttpMethod.Post, url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    DebugLog.WriteLine("[EventQueueService] [TrySendToServerAsync] Success");
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

        private void SafeWriteToFile(string filePath, string content)
        {
            try
            {
                var tmpPath = filePath + ".tmp";
                File.WriteAllText(tmpPath, content);
                File.Replace(tmpPath, filePath, null);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[EventQueueService] [SafeWriteToFile] Error writing to file: {ex.Message}");
            }
        }
    }
}

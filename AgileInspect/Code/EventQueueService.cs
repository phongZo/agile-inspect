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
            var eventType = StoreCfgLoader.mapPluginNameToEventType(pluginName);
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return;
            }
            EnqueueByEventType(eventType, json);
        }

        public void EnqueueByEventType(string eventType, JToken json)
        {
            if (string.Equals(StoreCfgJson.Instance.deployType, "serverless", StringComparison.OrdinalIgnoreCase))
                return;

            var saveEvent = new SaveEvent
            {
                timestampUtc = DateTime.UtcNow,
                eventType = eventType,
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

            // FIFO: send in order, stop on first failure.
            // Remove-by-id so new identical events never get dropped.
            var sentOkIds = new HashSet<string>(StringComparer.Ordinal);
            bool hitFailure = false;
            int failIndex = -1;

            for (int i = 0; i < originalQueue.Count; i++)
            {
                var item = originalQueue[i];
                var json = JsonConvert.SerializeObject(item);
                DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Sending JSON: {json}");

                bool success = await TrySendToServerAsync(json);
                if (success)
                {
                    DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Send success.");
                    var id = (string)item["eventId"];
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        sentOkIds.Add(id);
                    }
                }
                else
                {
                    DebugLog.WriteLine("[EventQueueService] [SendQueueAsync] Send failed. Will retry next time.");
                    hitFailure = true;
                    failIndex = i;
                    break;
                }
            }

            lock (_lock)
            {
                // Reload latest (may include new Enqueue while sending).
                List<JToken> currentQueue;
                try
                {
                    var latest = File.ReadAllText(_filePath);
                    currentQueue = JsonConvert.DeserializeObject<List<JToken>>(latest) ?? new List<JToken>();
                }
                catch
                {
                    currentQueue = new List<JToken>();
                }

                // Remove items we sent OK from current queue (eventId).
                if (sentOkIds.Count > 0 && currentQueue.Count > 0)
                {
                    var remaining = new List<JToken>(currentQueue.Count);
                    foreach (var t in currentQueue)
                    {
                        var id = (string)t["eventId"];
                        if (!string.IsNullOrWhiteSpace(id) && sentOkIds.Contains(id))
                        {
                            continue; // drop (already sent OK)
                        }
                        remaining.Add(t);
                    }
                    currentQueue = remaining;
                }

                // If we hit failure, ensure unsent suffix from snapshot is still present (in order).
                // Needed if queue file got corrupted/reset while we were sending.
                if (hitFailure && failIndex >= 0)
                {
                    // Build multiset of currentQueue items (by JSON) so we only append missing ones.
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var t in currentQueue)
                    {
                        var s = JsonConvert.SerializeObject(t);
                        if (counts.TryGetValue(s, out var c)) counts[s] = c + 1;
                        else counts[s] = 1;
                    }

                    for (int i = failIndex; i < originalQueue.Count; i++)
                    {
                        var t = originalQueue[i];
                        var s = JsonConvert.SerializeObject(t);
                        if (counts.TryGetValue(s, out var c) && c > 0)
                        {
                            counts[s] = c - 1;
                            continue; // already present
                        }
                        currentQueue.Add(t); // append missing
                    }
                }

                var updatedContent = JsonConvert.SerializeObject(currentQueue);
                WriteAllTextAtomic(_filePath, updatedContent);
                DebugLog.WriteLine($"[EventQueueService] [SendQueueAsync] Updated queue. Remaining: {currentQueue.Count}");
            }
        }

        private static void WriteAllTextAtomic(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content);

            try
            {
                // Atomic replace on Windows.
                File.Replace(tmp, path, null);
            }
            catch
            {
                // Fallback (not strictly atomic, but better than silent fail).
                File.Move(tmp, path, true);
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

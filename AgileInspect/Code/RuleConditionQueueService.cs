using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgileInspect.Code
{
    public class RuleConditionQueueService
    {
        #region Singleton
        public static RuleConditionQueueService Instance { get; private set; }
        public RuleConditionQueueService()
        {
            Instance = this;
            _queue = new Queue<string>();
        }
        #endregion

        private readonly object _lock = new();
        private Queue<string> _queue;

        public void EnqueueMatchedConditions(List<string> plugins, List<Condition> matchedConditions)
        {
            var json = JsonSerializer.Serialize(new
            {
                Plugins = plugins,
                Conditions = matchedConditions.Select(c => new { c.Field, c.Expected })
            });

            lock (_lock)
            {
                _queue.Enqueue(json);
            }
        }

        public async Task SendQueueAsync()
        {
            string jsonToSend = null;

            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
                    return;
                }
                jsonToSend = _queue.Peek();
            }

            DebugLog.WriteLine($"[SendQueueAsync] Sending JSON: {jsonToSend}");

            bool success = await TrySendToServerAsync(jsonToSend);

            if (success)
            {
                lock (_lock)
                {
                    if (_queue.Count > 0)
                    {
                        _queue.Dequeue();
                        DebugLog.WriteLine($"[SendQueueAsync] Send success. Messages left in queue: {_queue.Count}");
                    }
                }
            }
            else
            {
                DebugLog.WriteLine($"[SendQueueAsync] Send failed. JSON: {jsonToSend}");
                lock (_lock)
                {
                    DebugLog.WriteLine($"[SendQueueAsync] Messages left in queue: {_queue.Count}");
                }
            }
        }

        private async Task<bool> TrySendToServerAsync(string json)
        {
            try
            {
                using var client = new HttpClient();
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://your-server-endpoint/api/rule-match", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[TrySendToServerAsync] Exception: {ex.Message}");
                return false;
            }
        }

    }
}

using AgileInspect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class RuleConditionQueueService
{
    #region Singleton
    public static RuleConditionQueueService Instance { get; private set; }
    public RuleConditionQueueService()
    {
        Instance = this;
    }
    #endregion

    private readonly object _lock = new();
    string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgileInspect",
            "rule_queue.json"
        );

    public void EnqueueMatchedConditions(List<string> plugins, List<Condition> matchedConditions)
    {

        var entry = new
        {
            Plugins = plugins,
            Conditions = matchedConditions.Select(c => new { c.Field, c.Expected })
        };

        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, "[]");
            }

            List<object> queue = new();

            var existing = File.ReadAllText(_filePath);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                queue = JsonSerializer.Deserialize<List<object>>(existing) ?? new List<object>();
            }

            queue.Add(entry);
            var json = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }

    public async Task SendQueueAsync()
    {
        List<JsonElement> queue;

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
                DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
                return;
            }

            queue = JsonSerializer.Deserialize<List<JsonElement>>(content) ?? new();
        }

        if (queue.Count == 0)
        {
            DebugLog.WriteLine("[SendQueueAsync] Queue is empty, nothing to send.");
            return;
        }

        var first = queue[0];
        var json = first.GetRawText();
        DebugLog.WriteLine($"[SendQueueAsync] Sending JSON: {json}");

        bool success = await TrySendToServerAsync(json);

        if (success)
        {
            DebugLog.WriteLine("[SendQueueAsync] Send success.");
            lock (_lock)
            {
                queue.RemoveAt(0);
                var newContent = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, newContent);
                DebugLog.WriteLine($"[SendQueueAsync] Messages left in queue: {queue.Count}");
            }
        }
        else
        {
            DebugLog.WriteLine($"[SendQueueAsync] Send failed. Will retry later, messages left in queue: {queue.Count}");
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

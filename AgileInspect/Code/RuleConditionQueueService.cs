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
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgileInspect",
            "rule_queue.json"
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

    public void EnqueueMatchedConditions(List<string> plugins, List<Condition> matchedConditions)
    {
        var entry = new
        {
            Plugins = plugins,
            Conditions = matchedConditions.Select(c => new { c.field, c.expected })
        };

        lock (_lock)
        {
            var existing = File.ReadAllText(_filePath);
            List<JsonElement> queue = JsonSerializer.Deserialize<List<JsonElement>>(existing);

            var entryElement = JsonSerializer.SerializeToElement(entry);
            queue.Add(entryElement);

            var json = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
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
            DebugLog.WriteLine($"[SendQueueAsync] Sending JSON: {json}");

            bool success = await TrySendToServerAsync(json);
            if (success)
            {
                DebugLog.WriteLine("[SendQueueAsync] Send success.");
            }
            else
            {
                DebugLog.WriteLine("[SendQueueAsync] Send failed. Will retry next time.");
                newQueue.Add(item); // Keep to retry at next interval hit
            }
        }

        lock (_lock)
        {
            var updatedContent = JsonSerializer.Serialize(newQueue, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, updatedContent);
            DebugLog.WriteLine($"[SendQueueAsync] Updated queue. Remaining: {newQueue.Count}");
        }
    }

    private async Task<bool> TrySendToServerAsync(string json)
    {
        try
        {
            string url = StoreCfgJson.Instance.serverUrl;
            DebugLog.WriteLine($"[TrySendToServerAsync] URL: {url}");

            using var client = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLog.WriteLine($"[TrySendToServerAsync] Exception: {ex.Message}");
            return false;
        }
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace AgileInspect.Code.Settings
{
    internal class StoreCfgLoader
    {
        private const string ConfigFileName = "store.cfg";

        public static void Load()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(basePath, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    DebugLog.WriteLine($"Config file not found: {configPath}");
                    StoreCfgJson.Instance = new StoreCfgJson(); // fallback default
                    return;
                }

                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                StoreCfgJson.Instance = JsonSerializer.Deserialize<StoreCfgJson>(json, options)
                                        ?? new StoreCfgJson();

                DebugLog.WriteLine($"Loaded store config from {configPath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load store config: {ex.Message}");
                StoreCfgJson.Instance = new StoreCfgJson(); // fallback
            }
        }

        public static void Save()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(basePath, ConfigFileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(StoreCfgJson.Instance, options);
                File.WriteAllText(configPath, json);

                DebugLog.WriteLine($"Saved store config to {configPath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to save store config: {ex.Message}");
            }
        }
    }
}

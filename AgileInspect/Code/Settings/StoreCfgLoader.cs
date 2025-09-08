using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgileInspect.Code.Settings
{
    public class StoreCfgLoader
    {
        #region Singleton

        public static readonly StoreCfgLoader Instance = new();
        private StoreCfgLoader() { }
        #endregion
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
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cfg = JsonSerializer.Deserialize<StoreCfgJson>(json, options) ?? new StoreCfgJson();
                StoreCfgJson.SetCurrentStoreConfig(cfg);

                DebugLog.WriteLine($"Loaded store config from {configPath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load store config: {ex.Message}");
                var defaultConfig = new StoreCfgJson();
                StoreCfgJson.SetCurrentStoreConfig(defaultConfig);
                DebugLog.WriteLine($"Fallback defaut store config success");

                StoreCfgJson.Instance = new StoreCfgJson(); // fallback
            }
        }

        public void Save(StoreCfgJson config)
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(basePath, ConfigFileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);

                StoreCfgJson.SetCurrentStoreConfig(config);

                DebugLog.WriteLine($"Saved store config to {configPath}");
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to save store config: {ex.Message}");
            }
        }
        public StoreCfgJson Get()
        {
            return StoreCfgJson.GetCurrentStoreConfig();
        }
        public static string mapPluginNameToEventType(string pluginName)
        {
            if (pluginName == "WatermarkDetectorPlugin")
            {
                return "watermark";
            }
            else if (pluginName == "BrightnessChangePlugin")
            {
                return "brightness_change";
            }
            else
                return "";
        }
    }
}

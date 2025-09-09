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
        private const string ConfigFileName = "inspect_store.cfg";
        private string GetRoamingConfigPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
            Directory.CreateDirectory(folder); // ensure folder exists
            return Path.Combine(folder, ConfigFileName);
        }

        private string GetExeConfigPath()
        {
            string exePath = Environment.ProcessPath!;
            string baseDir = Path.GetDirectoryName(exePath)!;
            return Path.Combine(baseDir, ConfigFileName);
        }

        public StoreCfgJson Load()
        {
            string roamingPath = GetRoamingConfigPath();
            string exePath = GetExeConfigPath();

            try
            {
                string configPath = File.Exists(roamingPath) ? roamingPath : exePath;

                if (!File.Exists(configPath))
                {
                    DebugLog.WriteLine($"Config file not found at either location: {roamingPath}, {exePath}");

                    var defaultConfig = new StoreCfgJson();
                    Save(defaultConfig);
                    StoreCfgJson.SetCurrentStoreConfig(defaultConfig);
                    return defaultConfig;
                }

                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cfg = JsonSerializer.Deserialize<StoreCfgJson>(json, options) ?? new StoreCfgJson();

                StoreCfgJson.SetCurrentStoreConfig(cfg);
                DebugLog.WriteLine($"Loaded store config from {configPath}");
                return cfg;
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load store config: {ex.Message}");

                // fallback default config
                var defaultConfig = new StoreCfgJson();
                StoreCfgJson.SetCurrentStoreConfig(defaultConfig);
                DebugLog.WriteLine($"Fallback defaut store config success");

                return defaultConfig;
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
            else if (pluginName == "ProcessMonitorPlugin")
            {
                return "process_monitor";
            }
            else if (pluginName == "ServiceMonitorPlugin")
            {
                return "service_monitor";
            }
            else if (pluginName == "FileChangePlugin")
            {
                return "file_changed";
            }
            else if (pluginName == "NetworkChangePlugin")
            {
                return "network_change";
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

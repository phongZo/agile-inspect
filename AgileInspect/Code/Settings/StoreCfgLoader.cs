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
        FileStream FileLockStream;
        public void LockFile()
        {
            UnlockFile();
            FileLockStream = new FileStream(ConfigFileName, FileMode.Open, FileAccess.Read);
        }
        public void UnlockFile()
        {
            Permission.Instance.ResetPermissionOfFile(ConfigFileName);
            if (FileLockStream != null)
            {
                FileLockStream.Close();
                FileLockStream = null;
            }
        }
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

        public bool CopyConfigFromExeToRoaming(bool onlyIfMissingOrOlder = true)
        {
            string src = GetExeConfigPath();      // ...\inspect_store.cfg
            string dst = GetRoamingConfigPath();  // %AppData%\AgileInspect\inspect_store.cfg

            DebugLog.WriteLine($"[CopyCfg] Source: {src}");
            DebugLog.WriteLine($"[CopyCfg] Destination: {dst}");

            string? tmp = null;
            try
            {
                UnlockFile();

                if (!File.Exists(src))
                {
                    DebugLog.WriteLine("[CopyCfg] Source not found. Abort.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                if (onlyIfMissingOrOlder && File.Exists(dst))
                {
                    var s = new FileInfo(src);
                    var d = new FileInfo(dst);

                    if (d.LastWriteTimeUtc >= s.LastWriteTimeUtc)
                    {
                        DebugLog.WriteLine("[CopyCfg] Destination is up-to-date. Skip.");
                        return true;
                    }
                }

                tmp = dst + ".tmp";
                if (File.Exists(tmp)) File.Delete(tmp);

                File.Copy(src, tmp, true);

                File.Move(tmp, dst, true);

                File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));

                DebugLog.WriteLine($"[CopyCfg] Copied {ConfigFileName} → Roaming OK.");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[CopyCfg] Failed: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch { }
                try { LockFile(); } catch { }
            }
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

                    try { CopyConfigFromExeToRoaming(onlyIfMissingOrOlder: true); } catch { }

                    return defaultConfig;
                }

                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cfg = JsonSerializer.Deserialize<StoreCfgJson>(json, options) ?? new StoreCfgJson();

                StoreCfgJson.SetCurrentStoreConfig(cfg);
                DebugLog.WriteLine($"Loaded store config from {configPath}");

                try
                {
                    CopyConfigFromExeToRoaming(onlyIfMissingOrOlder: true);
                }
                catch (Exception copyEx)
                {
                    DebugLog.WriteLine($"Copy-after-load failed: {copyEx.Message}");
                }

                return cfg;
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Failed to load store config: {ex.Message}");

                // fallback default config
                var defaultConfig = new StoreCfgJson();
                StoreCfgJson.SetCurrentStoreConfig(defaultConfig);
                DebugLog.WriteLine("Fallback default store config success");

                try { CopyConfigFromExeToRoaming(onlyIfMissingOrOlder: true); } catch { }

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
                UnlockFile();
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
                LockFile();
                StoreCfgJson.SetCurrentStoreConfig(config);

                DebugLog.WriteLine($"Saved store config to {configPath}");

                try
                {
                    bool copied = CopyConfigFromExeToRoaming(onlyIfMissingOrOlder: true);
                    DebugLog.WriteLine($"Copy after save: {(copied ? "success" : "skipped/failed")}");
                }
                catch (Exception copyEx)
                {
                    DebugLog.WriteLine($"Copy-after-save failed: {copyEx.Message}");
                }
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

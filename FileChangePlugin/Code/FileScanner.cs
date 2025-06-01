using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileChangePlugin
{
    public class FileScanner
    {
        #region Singleton
        public static FileScanner Instance { get; set; }
        public FileScanner()
        {
            Instance = this;
        }
        #endregion

        public List<TrackedDirectory> LastTrackedDirs = [];
        public List<TrackedDirectory> TrackedDirs = [];
        public bool IsScanning = false;
        private const string CacheFile = "scan_cache.json";
        private string LastCacheHash;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public string Name => "FileChangePlugin";

        public void HandleScan()
        {
            try
            {
                var cfg = StoreCfgJson.Instance;
                var eventSetting = cfg.EventSetting;

                var eventParams = eventSetting.EventParams;
                string[] scanDirs = eventParams?.Paths;
                string[] filters = eventParams?.Filters;

                if (scanDirs == null || scanDirs.Length == 0)
                {
                    PluginContext.Log(Name,"[FileScanner] No directories specified for scanning. Skipping scan.");
                    return;
                }

                if (filters == null || filters.Length == 0)
                {
                    PluginContext.Log(Name, "[FileScanner] No filters specified for scanning. Skipping scan.");
                    return;
                }

                PluginContext.Log(Name, "[FileScanner] Running scan with filters: " + string.Join(", ", filters));

                TrackedDirs.Clear();
                LoadScanCache();
                HashSet<string> allFiles = new HashSet<string>();

                foreach (string scanDir in scanDirs)
                {
                    if (!Directory.Exists(scanDir))
                    {
                        PluginContext.Log(Name, $"[FileScanner] Directory does not exist: {scanDir}");
                        continue;
                    }
                    ScanSingleDirectory(scanDir, ref allFiles);
                }

                SaveScanCache();
                int totalFiles = TrackedDirs.Sum(d => d.Files.Count);
                PluginContext.Log(Name, $"[FileScanner] Total tracked files: {totalFiles}");
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileScanner] Exception during scanning process: {ex.Message}");
            }
        }

        private void ScanSingleDirectory(string scanDir, ref HashSet<string> allFiles)
        {
            PluginContext.Log(Name, $"[FileScanner] Starting scan in directory: {scanDir}");

            List<string> dirFiles = ScanDirectoryIterative(scanDir);
            if (dirFiles == null) return;

            TrackedDirectory newTrackedDir = new TrackedDirectory
            {
                DirectoryPath = scanDir,
                Files = new List<TrackedFile>()
            };
            TrackedDirs.Add(newTrackedDir);

            TrackedDirectory lastTrackedDir = LastTrackedDirs
                .FirstOrDefault(d => d.DirectoryPath == scanDir);

            HashSet<string> oldFileSet = lastTrackedDir != null
                ? new HashSet<string>(lastTrackedDir.Files.Select(f => f.FilePath))
                : new HashSet<string>();

            foreach (string filePath in dirFiles)
            {
                if (!FileWatcher.Instance.ShouldTrackFile(filePath))
                {
                    continue;
                }
                ProcessFile(filePath, lastTrackedDir, newTrackedDir, ref oldFileSet, ref allFiles);
            }

            foreach (string deletedFile in oldFileSet)
            {
                PluginContext.Log(Name, " - DELETED: " + deletedFile);
            }
        }

        private void ProcessFile(string filePath, TrackedDirectory lastTrackedDir, TrackedDirectory newTrackedDir,
                                 ref HashSet<string> oldFileSet, ref HashSet<string> allFiles)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                DateTime lastWrite = fileInfo.LastWriteTime;
                allFiles.Add(filePath);

                TrackedFile oldFile = lastTrackedDir?.Files
                    .FirstOrDefault(f => f.FilePath == filePath);

                if (oldFile == null)
                {
                    PluginContext.Log(Name, " - NEW: " + filePath);
                }
                else if (lastWrite > oldFile.LastWriteTime)
                {
                    PluginContext.Log(Name, " - MODIFIED: " + filePath);
                }

                newTrackedDir.Files.Add(new TrackedFile
                {
                    FilePath = filePath,
                    LastWriteTime = oldFile != null && oldFile.LastWriteTime > lastWrite
                        ? oldFile.LastWriteTime
                        : lastWrite
                });

                oldFileSet.Remove(filePath);
            }
            catch (Exception exFile)
            {
                PluginContext.Log(Name, "[FileScanner] Error accessing file '" + filePath + "': " + exFile.Message);
            }
        }

        public void SaveScanCache()
        {
            try
            {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileChangePlugin\\";
                Directory.CreateDirectory(folder);
                string cacheFilePath = Path.Combine(folder, CacheFile);

                string dataJson = JsonSerializer.Serialize(TrackedDirs);

                string hash = ComputeSHA256Hash(dataJson);

                if (LastCacheHash == hash)
                {
                    PluginContext.Log(Name, "[FileScanner] Cache hash is unchanged. No need to save.");
                    return;
                }
                var wrapper = new CacheWrapper<List<TrackedDirectory>>
                {
                    Data = TrackedDirs,
                    Hash = hash
                };

                var optionsIndented = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string wrapperJson = JsonSerializer.Serialize(wrapper, optionsIndented);

                File.WriteAllText(cacheFilePath, wrapperJson);

                LastCacheHash = hash;

                PluginContext.Log(Name, "[FileScanner] Scan cache with hash saved.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileScanner] Error saving scan cache: {ex.Message}");
            }
        }

        private string ComputeSHA256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public void LoadScanCache()
        {
            try
            {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileChangePlugin\\";
                string cacheFilePath = Path.Combine(folder, CacheFile);

                if (File.Exists(cacheFilePath))
                {
                    string json = File.ReadAllText(cacheFilePath);
                    var wrapper = JsonSerializer.Deserialize<CacheWrapper<List<TrackedDirectory>>>(json);

                    if (wrapper != null && wrapper.Data != null)
                    {
                        string rawDataJson = JsonSerializer.Serialize(wrapper.Data);
                        string recomputedHash = ComputeSHA256Hash(rawDataJson);

                        if (recomputedHash == wrapper.Hash)
                        {
                            LastTrackedDirs = new List<TrackedDirectory>(wrapper.Data);
                            LastCacheHash = wrapper.Hash;

                            PluginContext.Log(Name, "[FileScanner] Cache loaded and hash verified.");
                        }
                        else
                        {
                            PluginContext.Log(Name, "[FileScanner] Hash mismatch!");
                        }
                    }
                    else
                    {
                        PluginContext.Log(Name, "[FileScanner] Cache file structure is invalid.");
                    }
                }
                else
                {
                    PluginContext.Log(Name, "[FileScanner] No cache file found.");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileScanner] Error loading scan cache: {ex.Message}");
            }
        }

        public List<string> ScanDirectoryIterative(string rootPath)
        {
            List<string> matchedFiles = new List<string>();

            var eventParams = StoreCfgJson.Instance.EventSetting.EventParams as EventParams;
            string[] filters = eventParams?.Filters;

            if (filters == null || filters.Length == 0)
            {
                PluginContext.Log(Name, "[FileScanner] Filters setting is null or empty. No files will be matched.");
                return matchedFiles;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    if (!Permission.Instance.HasReadWritePermissions(file))
                        continue;

                    string dir = Path.GetDirectoryName(file);

                    if (!FileWatcher.Instance.IsScanDirectory(dir))
                        continue;

                    if (FileWatcher.Instance.IsTemporaryFile(file))
                        continue;

                    string fileExt = Path.GetExtension(file);
                    if (!filters.Contains(fileExt, StringComparer.OrdinalIgnoreCase))
                        continue;

                    matchedFiles.Add(file);
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileScanner] Error during scan: {ex.Message}");
            }

            return matchedFiles;
        }


        public class CacheWrapper<T>
        {
            [JsonPropertyName("Data")]
            public T Data { get; set; }

            [JsonPropertyName("Hash")]
            public string Hash { get; set; }
        }

        public class TrackedDirectory
        {
            [JsonPropertyName("DirectoryPath")]
            public string DirectoryPath { get; set; }

            [JsonPropertyName("Files")]
            public List<TrackedFile> Files { get; set; }
        }

        public class TrackedFile
        {
            [JsonPropertyName("FilePath")]
            public string FilePath { get; set; }

            [JsonPropertyName("LastWriteTime")]
            public DateTime LastWriteTime { get; set; }
        }
    }
}

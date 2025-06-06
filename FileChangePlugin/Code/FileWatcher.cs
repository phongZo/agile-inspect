using System.Text.RegularExpressions;
using static FileChangePlugin.FileScanner;

namespace FileChangePlugin
{
    public class FileWatcher
    {
        #region Singleton
        public static FileWatcher Instance { get; set; }
        public FileWatcher()
        {
            Instance = this;
        }

        #endregion
        private List<FileSystemEventArgs> PendingFileEvents = [];
        private readonly List<FileSystemWatcher> _watchers = [];
        public string Name => "FileChangePlugin";

        public void StartWatching(string path)
        {
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = path,
                Filter = "*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                InternalBufferSize = 64 * 1024,
            };

            watcher.Created += (sender, e) => OnCreated(sender, e, path);
            watcher.Changed += (sender, e) => OnChanged(sender, e, path);
            watcher.Deleted += (sender, e) => OnDeleted(sender, e, path);
            watcher.Renamed += (sender, e) => OnRenamed(sender, e, path);
            watcher.Error += (sender, e) =>
            {
                var ex = e.GetException();
                PluginContext.Log("FileWatcher", $"Watcher error: {ex?.Message}");
            };
            _watchers.Add(watcher); // must have to prevent the FileSystemWatcher from being garbage collected
        }

        public void ProcessPendingFileEvents()
        {
            foreach (var eventArgs in PendingFileEvents)
            {
                switch (eventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        OnCreated(this, eventArgs, Path.GetDirectoryName(eventArgs.FullPath));
                        break;

                    case WatcherChangeTypes.Renamed:
                        OnRenamed(this, (RenamedEventArgs)eventArgs, Path.GetDirectoryName(eventArgs.FullPath));
                        break;

                    case WatcherChangeTypes.Deleted:
                        OnDeleted(this, eventArgs, Path.GetDirectoryName(eventArgs.FullPath));
                        break;

                    case WatcherChangeTypes.Changed:
                        OnChanged(this, eventArgs, Path.GetDirectoryName(eventArgs.FullPath));
                        break;
                }
            }
            PendingFileEvents.Clear();
        }
        private void OnRenamed(object sender, RenamedEventArgs e, string scanDir)
        {
            if (FileScanner.Instance.IsScanning)
            {
                PendingFileEvents.Add(e);
                return;
            }

            try
            {
                var trackedDir = GetTrackedDir(scanDir);
                if (trackedDir == null) return;

                if (Directory.Exists(e.FullPath)) // Folder renamed
                {
                    PluginContext.Log(Name, $"[FileWatcher] Renamed folder from: {e.OldFullPath} to: {e.FullPath}");
                    if (!IsScanDirectory(e.FullPath)) return;

                    foreach (var trackedFile in trackedDir.Files.ToList())
                    {
                        if (!ShouldTrackFile(trackedFile.FilePath)) continue;

                        if (trackedFile.FilePath.StartsWith(e.OldFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string relativePath = trackedFile.FilePath.Substring(e.OldFullPath.Length);
                            string newFilePath = Path.Combine(e.FullPath, relativePath.TrimStart(Path.DirectorySeparatorChar));

                            RemoveFile(trackedDir, trackedFile.FilePath);
                            AddOrUpdateFile(trackedDir, newFilePath);

                            PluginContext.Log(Name, $"[FileWatcher] Renamed file from: {trackedFile.FilePath} to: {newFilePath}");
                        }
                    }
                }
                else if (File.Exists(e.FullPath) && ShouldTrackFile(e.FullPath)) // File renamed
                {
                    RemoveFile(trackedDir, e.OldFullPath);
                    AddOrUpdateFile(trackedDir, e.FullPath);

                    PluginContext.Log(Name, $"[FileWatcher] Renamed file from: {e.OldFullPath} to: {e.FullPath}");
                }

                FileScanner.Instance.SaveScanCache();
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileWatcher] Error in Renamed for {e.FullPath}: {ex.Message}");
            }
        }


        private void OnDeleted(object sender, FileSystemEventArgs e, string scanDir)
        {
            if (FileScanner.Instance.IsScanning)
            {
                PendingFileEvents.Add(e);
                return;
            }

            try
            {
                var trackedDir = GetTrackedDir(scanDir);
                if (trackedDir == null) return;

                if (!Path.HasExtension(e.FullPath)) // Directory
                {
                    PluginContext.Log(Name, $"[FileWatcher] Directory Deleted: {e.FullPath}");

                    var filesToRemove = trackedDir.Files
                        .Where(f => f.FilePath.StartsWith(e.FullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        .Where(f => ShouldTrackFile(f.FilePath))
                        .ToList();

                    foreach (var file in filesToRemove)
                    {
                        trackedDir.Files.Remove(file);
                        PluginContext.Log(Name, $"[FileWatcher] Deleted file in deleted directory: {file.FilePath}");
                    }
                }
                else // File
                {
                    if (ShouldTrackFile(e.FullPath))
                    {
                        RemoveFile(trackedDir, e.FullPath);
                        PluginContext.Log(Name, $"[FileWatcher] File Deleted: {e.FullPath}");
                    }
                }

                FileScanner.Instance.SaveScanCache();

            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileWatcher] Error in Deleted for {e.FullPath}: {ex.Message}");
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e, string scanDir)
        {
            try
            {
                if (FileScanner.Instance.IsScanning)
                {
                    PendingFileEvents.Add(e);
                    return;
                }
                // Dir
                var trackedDir = GetTrackedDir(scanDir);
                if (trackedDir == null) return;

                if (Directory.Exists(e.FullPath))
                {
                    PluginContext.Log(Name, $"[FileWatcher] Directory Created: {e.FullPath}");

                    var files = FileScanner.Instance.ScanDirectoryIterative(e.FullPath);
                    foreach (var file in files)
                    {
                        if (ShouldTrackFile(file))
                        {
                            AddOrUpdateFile(trackedDir, file);
                        }
                    }

                    FileScanner.Instance.SaveScanCache();
                }
                // file
                else if (File.Exists(e.FullPath) && ShouldTrackFile(e.FullPath))
                {
                    AddOrUpdateFile(trackedDir, e.FullPath);
                    PluginContext.Log(Name, $"[FileWatcher] File Created: {e.FullPath}");
                    FileScanner.Instance.SaveScanCache();
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileWatcher] Error in Created for {e.FullPath}: {ex.Message}");
            }
        }
        private void OnChanged(object sender, FileSystemEventArgs e, string scanDir)
        {
            if (FileScanner.Instance.IsScanning)
            {
                PendingFileEvents.Add(e);
                return;
            }

            if (!ShouldTrackFile(e.FullPath)) return;

            try
            {
                var trackedDir = GetTrackedDir(scanDir);
                if (trackedDir == null) return;

                AddOrUpdateFile(trackedDir, e.FullPath);
                PluginContext.Log(Name, $"[FileWatcher] Changed: {e.FullPath}");

                FileScanner.Instance.SaveScanCache();
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileWatcher] Error in Changed for {e.FullPath}: {ex.Message}");
            }
        }

        public bool IsScanDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var eventParams = StoreCfgJson.Instance.eventSetting.eventParams;
            if (eventParams?.paths == null || eventParams.paths.Length == 0)
                return false;

            var scanDirs = new HashSet<string>(eventParams.paths, StringComparer.OrdinalIgnoreCase);

            var directoriesInPath = new List<string>();
            var currentPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(currentPath))
            {
                directoriesInPath.Add(currentPath);
                currentPath = Path.GetDirectoryName(currentPath);
            }

            return directoriesInPath.Any(dir => scanDirs.Contains(dir));
        }

        public bool IsDetectedExtension(string filePath)
        {
            var eventParams = StoreCfgJson.Instance.eventSetting.eventParams;
            if (eventParams?.filters == null || eventParams.filters.Length == 0)
                return false;

            string ext = Path.GetExtension(filePath);

            var extensions = eventParams.filters
                .Select(f => f.StartsWith("*.") ? f.Substring(1) : f)
                .ToArray();

            return extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }


        public bool IsTemporaryFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath);

            // BYPASS SPECIAL CASES
            // Allow specific Word-generated temp file like ~WRDxxxx.tmp
            // Allow ~$ lock file (ex: Word, Excel ...)
            bool isBypass =
                Regex.IsMatch(fileName, @"^~WRD.*\.tmp$", RegexOptions.IgnoreCase) ||
                (fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase) && IsDetectedExtension(filePath));

            if (isBypass)
                return false;

            // TEMPORARY EXTENSIONS
            string[] tempExtensions = { ".tmp", ".bak", ".swp", ".part", ".crdownload", ".asd", ".wbk", ".~" };
            bool isTempByExtension = tempExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

            // FILE ATTRIBUTES
            bool isTempByAttribute = false;
            try
            {
                FileAttributes attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.Temporary) != 0)
                    isTempByAttribute = true;

                if ((attributes & FileAttributes.Hidden) != 0 && fileName.StartsWith("~"))
                    isTempByAttribute = true;
            }
            catch
            {
                // Can't read attributes
            }

            return isTempByExtension || isTempByAttribute;
        }

        // Stop 
        public void StopWatching()
        {
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    PluginContext.Log("FileWatcher", $"Failed to stop watcher: {ex.Message}");
                }
            }
            _watchers.Clear();
        }

        public bool ShouldTrackFile(string filePath)
        {
            if (!IsDetectedExtension(filePath)) return false;
            if (IsTemporaryFile(filePath)) return false;
            return true;
        }

        private TrackedDirectory GetTrackedDir(string scanDir)
        {
            return FileScanner.Instance.TrackedDirs
                .FirstOrDefault(d => string.Equals(d.DirectoryPath, scanDir, StringComparison.OrdinalIgnoreCase));
        }

        private void AddOrUpdateFile(TrackedDirectory dir, string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return;

            var existing = dir.Files
                .FirstOrDefault(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastWriteTime = fileInfo.LastWriteTime;
            }
            else
            {
                dir.Files.Add(new TrackedFile
                {
                    FilePath = filePath,
                    LastWriteTime = fileInfo.LastWriteTime
                });
            }
        }

        private void RemoveFile(TrackedDirectory dir, string filePath)
        {
            var file = dir.Files.FirstOrDefault(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                dir.Files.Remove(file);
            }
        }
    }
}

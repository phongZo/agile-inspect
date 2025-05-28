using System.Text.RegularExpressions;

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
        private FileSystemWatcher FileSystemWatcher = new();
        private List<FileSystemEventArgs> PendingFileEvents = new();
        public void StartWatching(string path)
        {
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = path,
                Filter = "*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };

            watcher.Created += (sender, e) => OnCreated(sender, e, path);
            watcher.Changed += (sender, e) => OnChanged(sender, e, path);
            watcher.Deleted += (sender, e) => OnDeleted(sender, e, path);
            watcher.Renamed += (sender, e) => OnRenamed(sender, e, path);
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
                    DebugLog.WriteLine($"[FileWatcher] Renamed folder from: {e.OldFullPath} to: {e.FullPath}");
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

                            DebugLog.WriteLine($"[FileWatcher] Renamed file from: {trackedFile.FilePath} to: {newFilePath}");
                        }
                    }
                }
                else if (File.Exists(e.FullPath) && ShouldTrackFile(e.FullPath)) // File renamed
                {
                    RemoveFile(trackedDir, e.OldFullPath);
                    AddOrUpdateFile(trackedDir, e.FullPath);

                    DebugLog.WriteLine($"[FileWatcher] Renamed file from: {e.OldFullPath} to: {e.FullPath}");
                }

                FileScanner.Instance.SaveScanCache();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[FileWatcher] Error in Renamed for {e.FullPath}: {ex.Message}");
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
                    DebugLog.WriteLine($"[FileWatcher] Directory Deleted: {e.FullPath}");

                    var filesToRemove = trackedDir.Files
                        .Where(f => f.FilePath.StartsWith(e.FullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        .Where(f => ShouldTrackFile(f.FilePath))
                        .ToList();

                    foreach (var file in filesToRemove)
                    {
                        trackedDir.Files.Remove(file);
                        DebugLog.WriteLine($"[FileWatcher] Deleted file in deleted directory: {file.FilePath}");
                    }
                }
                else // File
                {
                    if (ShouldTrackFile(e.FullPath))
                    {
                        RemoveFile(trackedDir, e.FullPath);
                        DebugLog.WriteLine($"[FileWatcher] File Deleted: {e.FullPath}");
                    }
                }

                FileScanner.Instance.SaveScanCache();

            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[FileWatcher] Error in Deleted for {e.FullPath}: {ex.Message}");
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
                    DebugLog.WriteLine($"[FileWatcher] Directory Created: {e.FullPath}");

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
                    DebugLog.WriteLine($"[FileWatcher] File Created: {e.FullPath}");
                    FileScanner.Instance.SaveScanCache();
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[FileWatcher] Error in Created for {e.FullPath}: {ex.Message}");
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
                DebugLog.WriteLine($"[FileWatcher] Changed: {e.FullPath}");

                FileScanner.Instance.SaveScanCache();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[FileWatcher] Error in Changed for {e.FullPath}: {ex.Message}");
            }
        }

        public bool IsScanDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var scanDirs = new HashSet<string>(StoreCfgJson.Instance.ScanDirectories ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var directoriesInPath = path
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Select(dir => Path.Combine(path.Substring(0, path.IndexOf(dir) + dir.Length)))
                .ToList();

            return directoriesInPath.Any(dir => scanDirs.Contains(dir));
        }


        public bool IsDetectedExtension(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            return StoreCfgJson.Instance.ScanExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
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
            FileSystemWatcher.EnableRaisingEvents = false;
            FileSystemWatcher.Dispose();
        }

        public bool ShouldTrackFile(string filePath)
        {
            if (!StoreCfgJson.Instance.IsFullScan)
            {
                if (!IsDetectedExtension(filePath)) return false;
                if (IsTemporaryFile(filePath)) return false;
            }
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

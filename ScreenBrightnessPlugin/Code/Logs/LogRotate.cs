using ScreenBrightnessPlugin.Code.Settings;
using System.IO;
using System.IO.Compression;

namespace ScreenBrightnessPlugin.Code.Logs
{
    public class LogRotate
    {
        public static string TemporaryLog { get; set; } = string.Empty;
        public static bool IsCompressing { get; set; } = false;
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();

        public static void HandleRotation()
        {
            try
            {
                string logFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileChangePlugin\\";
                string logFilePath = Path.Combine(logFolder, "filelog.txt");

                LogRotation logRotation = StoreCfgJson.Instance.LogRotation;
                FileInfo logFileInfo = new FileInfo(logFilePath);

                if (logFileInfo.Exists && logFileInfo.Length > logRotation.size)
                {
                    // Fire and forget but with error handling
                    Task.Run(() => HandleCompression(logFolder, logRotation));
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Error in HandleRotation: {ex.Message}");
            }
        }

        private static void HandleCompression(string logFolder, LogRotation logRotation)
        {

            // fileflog.txt.ddMMyyyyhhmmss.1.gz
            string timestamp = DateTime.Now.ToString("ddMMyyyyHHmmss");

            string tempLogFilePath = Path.Combine(logFolder, $"filelog.txt.{timestamp}.1.txt");
            try
            {
                IsCompressing = true;

                // Copy log to temporary file
                CopyLogToDestFile(tempLogFilePath);

                // Truncate log file
                TruncateLogFile();

                IsCompressing = false;

                // append temp DebugLog
                if (!string.IsNullOrEmpty(TemporaryLog))
                {
                    DebugLog.Filestream.Seek(0, SeekOrigin.End);
                    DebugLog.StreamWriter.Write(TemporaryLog);

                    TemporaryLog = string.Empty;
                }

                // Increase other version by 1
                RenameAndCleanupGZipFiles(logFolder, logRotation.rotate);

                // Compress the copied log
                CompressFile(tempLogFilePath);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"Error when process log rotation: {ex.Message}");
                // Handle errors and ensure no incomplete gz file remains
                if (File.Exists(tempLogFilePath))
                {
                    File.Delete(tempLogFilePath);
                }
            }
            IsCompressing = false;
        }

        private static void TruncateLogFile()
        {
            DebugLog.Filestream.SetLength(0);
        }

        private static void CopyLogToDestFile(string destinationPath)
        {
            using (FileStream destStream = File.Create(destinationPath))
            {
                DebugLog.Filestream.Seek(0, SeekOrigin.Begin);
                DebugLog.Filestream.CopyTo(destStream);
                DebugLog.Filestream.Seek(0, SeekOrigin.End);
            }
        }

        private static void CompressFile(string sourceFilePath)
        {
            try
            {
                // Create compressed file path by changing extension
                string compressedFilePath = Path.ChangeExtension(sourceFilePath, ".gz");

                // First compress the file
                using (FileStream sourceStream = File.OpenRead(sourceFilePath))
                using (FileStream compressedStream = File.Create(compressedFilePath))
                using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
                {
                    sourceStream.CopyTo(gzipStream);
                }

                // After successful compression, delete the source file
                File.Delete(sourceFilePath);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("Error during copy file: " + ex.Message);
            }
        }

        private static void RenameAndCleanupGZipFiles(string logFolder, int rotation)
        {
            var gzFiles = Directory.GetFiles(logFolder, "filelog.txt.*.gz")
                .Select(file => new FileInfo(file))
                .Select(file => new
                {
                    File = file,
                    Version = GetVersionOfGZip(file.Name)
                })
                .Where(x => x.Version.HasValue)
                .OrderBy(x => x.Version.Value)
                .Select(x => x.File)
                .ToList();

            int count = 2;
            var filesToDelete = new List<string>();

            foreach (var gzFile in gzFiles)
            {
                string currentGzPath = gzFile.FullName;
                string currentGzName = gzFile.Name;

                string nameWithoutExt = currentGzName.Substring(0, currentGzName.LastIndexOf('.'));
                string nameWithoutExtAndCount = nameWithoutExt.Substring(0, nameWithoutExt.LastIndexOf('.'));

                string newGzName = $"{nameWithoutExtAndCount}.{count}.gz";
                string newGzPath = Path.Combine(logFolder, newGzName);

                if (currentGzPath != newGzPath && !File.Exists(newGzPath))
                {
                    File.Move(currentGzPath, newGzPath);
                    if (count > rotation)
                    {
                        filesToDelete.Add(newGzPath);
                    }
                }

                count++;
            }

            foreach (var file in filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static int? GetVersionOfGZip(string fileName)
        {
            var parts = fileName.Split('.');
            if (parts.Length > 2 && int.TryParse(parts[parts.Length - 2], out int version))
            {
                return version;
            }
            return null;
        }
    }
}

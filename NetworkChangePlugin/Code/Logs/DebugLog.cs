namespace NetworkChangePlugin
{
    public static class DebugLog
    {
        public const string DateTimeFormat = "dd/MM/yy HH:mm:ss";
        public static StreamWriter StreamWriter;
        public static FileStream Filestream;
        public static bool CanWrite = true;
        private static readonly object _lock = new();

        public static void Init()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
                Directory.CreateDirectory(folder);
                Permission.Instance.ResetPermissionRoamingDirectory();

                Filestream = new FileStream(Path.Combine(folder, "networkchangelog.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                StreamWriter = new StreamWriter(Filestream) { AutoFlush = true };
                Console.SetError(StreamWriter);

                CanWrite = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("UNABLE TO WRITE networkchangelog.TXT");
                Console.WriteLine(e.Message);
                CanWrite = false;
            }
        }

        public static void Write(string message, bool timestamp = true)
        {
            if (!CanWrite) return;

            lock (_lock)
            {
                try
                {
                    if (timestamp)
                        Console.WriteLine($"{DateTime.Now.ToString(DateTimeFormat)}:   {message}");
                    else
                        Console.WriteLine(message);

                    if (StreamWriter == null) return;

                    if (LogRotate.IsCompressing)
                    {
                        LogRotate.TemporaryLog += (timestamp ? DateTime.Now.ToString(DateTimeFormat) + ":   " : "") + message + Environment.NewLine;
                        return;
                    }

                    Permission.Instance.ResetPermissionOfFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect", "networkchangelog.txt"));

                    Filestream?.Seek(0, SeekOrigin.End);

                    if (timestamp)
                        StreamWriter.WriteLine($"{DateTime.Now.ToString(DateTimeFormat)}:   {message}");
                    else
                        StreamWriter.WriteLine(message);
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (StreamWriter != null)
                        {
                            StreamWriter.WriteLine($"{DateTime.Now.ToString(DateTimeFormat)}:   Error when write to log: {ex.Message}");
                        }
                    }
                    catch { /* swallow */ }
                }
            }
        }

        public static void WriteLine(string message, bool timestamp = true)
        {
            Write(message, timestamp);
        }

        public static void WriteLine(string funcName, string actionName, string message)
        {
            WriteLine($"[{funcName} -> {actionName}]: {message}");
        }

        public static void WriteLine(string funcName, string message)
        {
            WriteLine($"[{funcName}]: {message}");
        }
    }
}

using AgileInspect.Code;
using System.Diagnostics;

namespace AgileInspect
{
    public static class DebugLog
    {
        public const string DateTimeFormat = "dd/MM/yy HH:mm:ss";
        public static StreamWriter StreamWriter;
        public static FileStream Filestream;
        public static bool CanWrite = true;
        private static readonly object _logLock = new();

        public static void Init()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
                Directory.CreateDirectory(folder);
                Permission.Instance.ResetPermissionRoamingDirectory();

                Filestream = new FileStream(Path.Combine(folder, "agileinspect_debug_log.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                StreamWriter = new StreamWriter(Filestream) { AutoFlush = true };
                Console.SetError(StreamWriter);

                CanWrite = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("UNABLE TO WRITE agileinspect_debug_log.TXT");
                Console.WriteLine(e.Message);
                CanWrite = false;
            }
        }

        public static void Write(string message, bool timestamp = true)
        {
            try
            {
                if (!DebugLog.CanWrite) return;
                lock (_logLock)
                {
                    Debug.WriteLine(message);
                    Console.WriteLine(message);
                    if (StreamWriter == null) return;
                    if (LogRotate.IsCompressing)
                    {
                        LogRotate.TemporaryLog += (timestamp ? DateTime.Now.ToString(DateTimeFormat) + ":   " : "") + message + Environment.NewLine;
                        return;
                    }

                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "AgileInspect",
                        "log.txt"
                    );

                    Permission.Instance.ResetPermissionOfFile(logPath);

                    Filestream.Seek(0, SeekOrigin.End);
                    if (timestamp) StreamWriter.Write(DateTime.Now.ToString(DateTimeFormat) + ":   ");
                    StreamWriter.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                lock (_logLock)
                {
                    if (timestamp) StreamWriter?.Write(DateTime.Now.ToString(DateTimeFormat) + ":   ");
                    StreamWriter?.WriteLine("Error when write to log: " + ex.Message);
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

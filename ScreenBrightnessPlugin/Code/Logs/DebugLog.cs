namespace ScreenBrightnessPlugin.Code.Logs
{
    public static class DebugLog
    {
        public const string DateTimeFormat = "dd/MM/yy HH:mm:ss";
        public static StreamWriter StreamWriter;
        private static bool CanWrite = true;
        public static FileStream Filestream;
        public static void Init()
        {
            try
            {
                string Folder = "";
                Folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\AgileInspect\\";
                Directory.CreateDirectory(Folder);
                Permission.Instance.ResetPermissionRoamingDirectory();

                Filestream = new FileStream(Folder + "screenbrightnesslog.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                StreamWriter = new StreamWriter(Filestream)
                {
                    AutoFlush = true
                };
                Console.SetError(StreamWriter);

                CanWrite = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("UNABLE TO WRITE screenbrightnesslog.txt");
                Console.WriteLine(e.Message);
                CanWrite = false;
            }
        }

        public static void Write(string message, bool timestamp = true)
        {
            try
            {
                if (!CanWrite) return;
                Console.WriteLine(message);
                if (StreamWriter == null) return;
                if (LogRotate.IsCompressing)
                {
                    LogRotate.TemporaryLog += (timestamp ? DateTime.Now.ToString(DateTimeFormat) + ":   " : "") + message + Environment.NewLine;
                    return;
                }
                Permission.Instance.ResetPermissionOfFile(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\AgileInspect\\" + "screenbrightnesslog.txt");

                Filestream.Seek(0, SeekOrigin.End);
                if (timestamp) StreamWriter.Write(DateTime.Now.ToString(DateTimeFormat) + ":   ");
                StreamWriter.WriteLine(message);
            }
            catch (Exception ex)
            {
                if (timestamp) StreamWriter.Write(DateTime.Now.ToString(DateTimeFormat) + ":   ");
                StreamWriter.WriteLine("Error when write to log: " + ex.Message);
            }
        }

        public static void WriteLine(string message, bool timestamp = true)
        {
            Write(message, timestamp);
        }

        // Structual logging
        // Action Name like: REQUEST, RESPONSE
        // Function Name: What do we do?
        // Message: more detail
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

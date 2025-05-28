namespace FileChangePlugin
{
    public class StoreCfgJson
    {
        #region Singleton
        public static StoreCfgJson Instance { get; set; }
        public StoreCfgJson()
        {
            Instance = this;
        }
        #endregion

        public LogRotation LogRotation { get; set; } = new LogRotation();
        public string[] ScanExtensions { get; set; } = new string[]
        {
            ".doc", ".docx", ".pdf", ".txt", ".xlsx"
        };
        public string[] ScanDirectories { get; set; } = new string[]
        {
            "D:\\SCAN","D:\\SCAN2"
        };
        public bool IsFullScan { get; set; } = false;
    }

    public class LogRotation
    {
        public bool enable { get; set; } = true;
        public int size { get; set; } = 10 * 1024 * 1024;
        public int rotate { get; set; } = 5;
    }
}

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
        public EventSetting EventSetting { get; set; } = new EventSetting();

    }

    public class LogRotation
    {
        public bool enable { get; set; } = true;
        public int size { get; set; } = 10 * 1024 * 1024;
        public int rotate { get; set; } = 5;
    }

    public class EventSetting
    {
        public EventParams EventParams { get; set; } = new EventParams();
        public string TriggerType { get; set; } = "Interval";
        public TriggerParams TriggerParams { get; set; } = new TriggerParams();
    }

    public class EventParams
    {
        public string[] Paths { get; set; } = [];
        public string[] Filters { get; set; } = [];
    }

    public class TriggerParams
    {
        public int Interval { get; set; } = 30;
    }


}

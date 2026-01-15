namespace FirewallDetectorPlugin
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
        public EventSetting eventSetting { get; set; } = new EventSetting();

    }

    public class LogRotation
    {
        public bool enable { get; set; } = true;
        public int size { get; set; } = 10 * 1024 * 1024;
        public int rotate { get; set; } = 5;
    }

    public class EventSetting
    {
        public EventParams eventParams { get; set; } = new EventParams();
        public string triggerType { get; set; } = "interval";
        public TriggerParams triggerParams { get; set; } = new TriggerParams();
    }

    public class EventParams
    {
    }

    public class TriggerParams
    {
        public int interval { get; set; } = 10;
    }
}

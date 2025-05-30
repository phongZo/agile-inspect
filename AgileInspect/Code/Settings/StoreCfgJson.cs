using System.Collections.Generic;

namespace AgileInspect
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
        public EventConfig EventConfig { get; set; } = new EventConfig();
    }

    public class LogRotation
    {
        public bool enable { get; set; } = true;
        public int size { get; set; } = 10 * 1024 * 1024;
        public int rotate { get; set; } = 5;
    }
    public class EventConfig
    {
        public List<EventSetting> EventSettings { get; set; } = new();
    }
    public class EventSetting
    {
        public string EventType { get; set; } // Plugin Name
        public object EventParams { get; set; }
        public string TriggerType { get; set; } // Interval or Realtime
        public object TriggerParams { get; set; }
    }
}

using System.Collections.Generic;

namespace PrintScreenDetectorPlugin.Code.Settings
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

        public EventSetting eventSetting { get; set; } = new EventSetting();
    }

    public class EventSetting
    {
        public EventParams eventParams { get; set; } = new EventParams();
        public string triggerType { get; set; } = "realtime";
        public TriggerParams triggerParams { get; set; } = new TriggerParams();
    }

    public class EventParams
    {
        public string[] processes { get; set; } = [];
    }

    public class TriggerParams
    {
        public int interval { get; set; } = 10;
    }
}

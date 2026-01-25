using System.Collections.Generic;

namespace AiInteractionDetectorPlugin.Code.Settings
{
    public class StoreCfgJson
    {
        public static StoreCfgJson Instance { get; set; } = new StoreCfgJson();
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
        public string[] browsers { get; set; } = [];
        
        public string[] aiDomains { get; set; } = [];
    }

    public class TriggerParams
    {
        public int interval { get; set; } = 2000;
    }
}

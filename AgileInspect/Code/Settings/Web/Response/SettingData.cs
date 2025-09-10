using System.Collections.Generic;

namespace AgileInspect.Code.Settings.Web.Response
{
    class SettingData
    {
        public EventData data { get; set; }
        public int statusCode { get; set; }
    }
    public class EventData
    {
        public List<EventSetting> eventSettings { get; set; } = new();
        public List<Rule> rules { get; set; } = new();
        public string settingHash { get; set; }
        public int settingPullInterval { get; set; }
    }
}

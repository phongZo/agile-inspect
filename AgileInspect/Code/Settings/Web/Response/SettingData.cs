using System.Collections.Generic;

namespace AgileInspect.Code.Settings.Web.Response
{
    class SettingData
    {
        public SettingWrapper data { get; set; }
        public int statusCode { get; set; }
    }
    class SettingWrapper
    {
        public List<EventSetting> eventSettings { get; set; }
        public List<AgileInspect.Rules> rules { get; set; }
        public string settingHash { get; set; } = "a";
        public int settingPullInterval { get; set; } = 30000;
    }
}

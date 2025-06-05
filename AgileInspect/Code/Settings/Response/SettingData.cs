using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgileInspect.Code.Settings.Response
{
    class SettingData
    {
        public EventData data {  get; set; }
        public int statusCode { get; set; }
    }
    class EventData
    {
        public EventSetting[] eventSettings { get; set; }
        public string settingHash { get; set; }
        public int settingPullInterval { get; set; }
    }
}

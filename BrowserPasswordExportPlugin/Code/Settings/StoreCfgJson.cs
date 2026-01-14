using System.Collections.Generic;

namespace BrowserPasswordExportPlugin.Code.Settings
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

        public List<string> targetProcesses { get; set; } = new List<string> { "chrome", "msedge", "brave", "firefox", "opera" };
        public List<string> titleKeywords { get; set; } = new List<string> { "save as", "export passwords", "xuất mật khẩu", "save" };
        public List<string> fileNameKeywords { get; set; } = new List<string> { "password", "pass", "login", "credentials", "mật khẩu" };
        public List<string> saveButtonKeywords { get; set; } = new List<string> { "save", "lưu", "download", "tải xuống" };
        public List<string> confirmOverwriteKeywords { get; set; } = new List<string> { "confirm save as", "confirm save", "xác nhận lưu", "confirm replace" };
        public List<string> yesButtonKeywords { get; set; } = new List<string> { "yes", "có", "ok" };

        public string fileExtension { get; set; } = ".csv";

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
        public string[] paths { get; set; } = [];
        public string[] filters { get; set; } = [];
        public string[] processes { get; set; } = [];
        public string[] services { get; set; } = [];
        public int activeTime { get; set; }
    }

    public class TriggerParams
    {
        public int interval { get; set; } = 10;
    }
}

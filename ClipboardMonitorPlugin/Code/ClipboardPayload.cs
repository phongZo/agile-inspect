using Newtonsoft.Json;

namespace ClipboardMonitorPlugin.Code
{
    public class ClipboardPayload
    {
        public string @event { get; set; } = "CLIPBOARD_UPDATED";
        public string type { get; set; } = "";
        public string preview { get; set; } = "";
        
        [JsonProperty("IsLabeled")]
        public bool IsLabeled { get; set; }
        
        [JsonProperty("IsRMSProtected")]
        public bool IsRMSProtected { get; set; }
        
        [JsonProperty("MainLabelName")]
        public string? MainLabelName { get; set; }
    }
}

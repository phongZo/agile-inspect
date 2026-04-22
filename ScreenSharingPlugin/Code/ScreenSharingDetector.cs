using System.Runtime.InteropServices;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace ScreenSharingPlugin
{
    public class ScreenSharingDetector
    {
        #region Singleton
        public static ScreenSharingDetector Instance { get; set; }
        public ScreenSharingDetector()
        {
            Instance = this;
        }
        #endregion

        private readonly string pluginName = "ScreenSharingPlugin";

        private const uint WS_VISIBLE = 0x10000000;
        private const int GWL_STYLE = -16;
        private const string ZoomSharingWindowClass = "cpt mag window";
        private const string ZoomSharingWindowName = "mag host";
        private const string SkypeAndTeamsSharingWindowClass = "ScreenBorderWindow";
        private const string WebexSharingWindowFullClass = "Qt5152QWindowToolSaveBits";
        private const string WebexSharingWindowFullName = "Webex";
        private const string WebexSharingWindowClass = "ApplicationBorderWindow";
        public void Check()
        {
            var activeSharingApps = DetectActiveSharingApps();
            bool detected = activeSharingApps.Count > 0;

            PluginContext.Log(pluginName, $"[ScreenSharing] Potential screen sharing active: {detected}");

            var resultObj = new JObject
            {
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                ["appList"] = JToken.FromObject(activeSharingApps)
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<string> DetectActiveSharingApps()
        {
            var result = new List<string>();

            try
            {
                if (DetectSkypeAndTeams())
                {
                    result.Add("Skype/Teams");
                }
                if (DetectZoom())
                {
                    result.Add("Zoom");
                }
                if (DetectWebex())
                {
                    result.Add("Webex");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ScreenSharing] Window scan error: {ex.Message}");
            }

            return result;
        }

        private static bool DetectSkypeAndTeams()
        {
            IntPtr handle = FindWindow(SkypeAndTeamsSharingWindowClass, string.Empty);
            return handle != IntPtr.Zero;
        }

        private static bool DetectZoom()
        {
            IntPtr handleSharingFrame = FindWindow("sharing frame", string.Empty);
            IntPtr handleWindow = FindWindow(ZoomSharingWindowClass, ZoomSharingWindowName);
            if (handleSharingFrame == IntPtr.Zero)
            {
                return false;
            }

            int style = GetWindowLong(handleSharingFrame, GWL_STYLE);
            return (style & WS_VISIBLE) != 0 || handleWindow != IntPtr.Zero;
        }

        private static bool DetectWebex()
        {
            IntPtr sharingWindow = FindWindow(WebexSharingWindowClass, string.Empty);
            if (sharingWindow != IntPtr.Zero)
            {
                return true;
            }

            IntPtr fullScreenWindow = FindWindow(WebexSharingWindowFullClass, WebexSharingWindowFullName);
            if (fullScreenWindow == IntPtr.Zero)
            {
                return false;
            }

            int style = GetWindowLong(fullScreenWindow, GWL_STYLE);
            return (style & WS_VISIBLE) != 0;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}

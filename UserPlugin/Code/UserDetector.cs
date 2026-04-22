using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;
using System.Security.Principal;

namespace UserPlugin
{
    public class UserDetector
    {
        #region Singleton
        public static UserDetector Instance { get; set; }
        public UserDetector()
        {
            Instance = this;
        }
        #endregion

        string pluginName = "UserPlugin";

        public void Check()
        {
            var userInfo = GetUserInfo();
            PluginContext.Log(pluginName, $"[User] Username: {userInfo.Username}, IsAdmin: {userInfo.IsAdmin}");

            var resultObj = new JObject
            {
                ["username"] = userInfo.Username,
                ["isAdmin"] = userInfo.IsAdmin,
                ["userType"] = userInfo.IsAdmin ? "admin" : "standard"
            };
            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private (string Username, bool IsAdmin) GetUserInfo()
        {
            try
            {
                string username = Environment.UserName;
                bool isAdmin = IsCurrentUserAdmin();
                return (username, isAdmin);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[User] Error getting user info: {ex.Message}");
                return (Environment.UserName, false);
            }
        }

        private bool IsCurrentUserAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}

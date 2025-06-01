using AgileInspect.Code.PluginContracts;

namespace AgileInspect
{
    public class AppCallbackHandler : IAppCallback
    {
        #region Singleton
        public static AppCallbackHandler Instance { get; set; }

        public AppCallbackHandler()
        {
            Instance = this;
        }
        #endregion

        public void OnLog(string pluginName, string message)
        {
            DebugLog.WriteLine($"[{pluginName}] {message}");
        }

        public void OnDetectionResult(string pluginName, int result)
        {
            DebugLog.WriteLine($"[{pluginName}] {result}");

        }
    }
}

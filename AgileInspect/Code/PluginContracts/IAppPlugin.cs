namespace AgileInspect.Code.PluginContracts
{
    public interface IAppCallback
    {
        void OnLog(string pluginName, string message);
        void OnDetectionResult(string pluginName, string jsonResult);

    }
    public interface IAppPlugin
    {
        string Name { get; }
        void Initialize();
        void Start();
        void Stop();
        void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson);
    }
    public interface IBrightnessChangePlugin : IAppPlugin
    {
    }
    public interface IWatermarkDetectorPlugin : IAppPlugin
    {
    }
    public interface IAntivirusDetectorPlugin : IAppPlugin
    {
    }
    public interface IFirewallDetectorPlugin : IAppPlugin
    {
    }
    public interface IInternetDetectorPlugin : IAppPlugin
    {
    }
}

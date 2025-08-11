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
    public interface IFileChangePlugin : IAppPlugin
    {
    }
    public interface INetworkChangePlugin : IAppPlugin
    {
    }
    public interface IBrightnessChangePlugin : IAppPlugin
    {
    }
    public interface IWatermarkDetectorPlugin : IAppPlugin
    {
    }
    public interface IProcessMonitorPlugin : IAppPlugin
    {
    }
    public interface IServiceMonitorPlugin : IAppPlugin
    {
    }
    public interface IInputMonitorPlugin : IAppPlugin
    {
    }
    public interface IResolutionDetectorPlugin : IAppPlugin
    {
    }
}

namespace AgileInspect.Code.PluginContracts
{
    public interface IAppPlugin
    {
        public string Name => GetType().Assembly.GetName().Name;
        void Initialize();
        void Start();
        void Stop();
    }
    public interface IFileChangePlugin : IAppPlugin
    {
    }
    public interface INetworkChangePlugin : IAppPlugin
    {
    }
    public interface IScreenBrightnessPlugin : IAppPlugin
    {
    }
}

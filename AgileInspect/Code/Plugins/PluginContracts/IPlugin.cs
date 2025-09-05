namespace AgileInspect.Code.Plugins.PluginContracts
{
    public interface IPlugin
    {
        string Name { get; }
        void Execute();
    }
}

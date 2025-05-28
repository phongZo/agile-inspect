using AgileInspect.Code.PluginContracts;
using NetworkChangePlugin.Code.Settings.Web;

namespace NetworkChangePlugin
{
    public class NetworkChangePlugin : INetworkChangePlugin
    {
        public NetworkAddressChangeWatcher NetworkAddressChangeWatcher { get; set; } = new NetworkAddressChangeWatcher();
        public Permission Permission { get; set; } = new Permission();
        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write("Initializing FileChangePlugin...", false);
        }

        public void Start()
        {
            DebugLog.Write("", false);
            DebugLog.Write("Starting NetworkChange Plugin...", false);
            DebugLog.Write("---------------------------------", false);

            NetworkAddressChangeWatcher.Instance.StartWatcher();
        }

        public void Stop()
        {
            
        }
    }
}

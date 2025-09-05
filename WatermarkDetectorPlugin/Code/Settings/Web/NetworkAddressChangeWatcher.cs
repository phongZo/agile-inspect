using System.Net.NetworkInformation;

namespace WatermarkDetectorPlugin.Code.Settings.Web
{
    public class NetworkAddressChangeWatcher
    {
        private static readonly NetworkAddressChangeWatcher _instance = new NetworkAddressChangeWatcher();
        public static NetworkAddressChangeWatcher Instance => _instance;

        private bool isWatching = false;
        private readonly string pluginName = "WatermarkDetectorPlugin";

        private NetworkAddressChangeWatcher() { }

        public void StartWatcher()
        {
            if (!isWatching)
            {
                NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangedCallback;
                isWatching = true;
                PluginContext.Log(pluginName, "NetworkAddressChangeWatcher started.");
            }
        }

        public void StopWatcher()
        {
            if (isWatching)
            {
                NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChangedCallback;
                isWatching = false;
                PluginContext.Log(pluginName, "NetworkAddressChangeWatcher stopped.");
            }
        }

        private void NetworkAvailabilityChangedCallback(object sender, NetworkAvailabilityEventArgs e)
        {
            PluginContext.Log(pluginName, $"Network {(e.IsAvailable ? "connected" : "disconnected")}");

            _ = Task.Run(async () =>
            {
                try
                {
                    await WatermarkDetector.Instance.ProcessAsync();
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"Detection failed in network callback: {ex}");
                }
            });
        }
    }
}

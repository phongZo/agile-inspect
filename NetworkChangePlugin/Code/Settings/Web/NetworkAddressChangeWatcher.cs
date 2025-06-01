using System.Net.NetworkInformation;

namespace NetworkChangePlugin.Code.Settings.Web
{
    public class NetworkAddressChangeWatcher
    {
        #region Singleton
        public static NetworkAddressChangeWatcher Instance { get; set; }
        public NetworkAddressChangeWatcher()
        {
            Instance = this;
        }
        #endregion

        private bool isWatching = false;
        string pluginName = "NetworkChangePlugin";

        public void StartWatcher()
        {
            if (!isWatching)
            {
                NetworkChange.NetworkAddressChanged += NetworkAddressChangedCallback;
                isWatching = true;
                PluginContext.Log(pluginName, "NetworkAddressChangeWatcher started.");
            }
        }

        public void StopWatcher()
        {
            if (isWatching)
            {
                NetworkChange.NetworkAddressChanged -= NetworkAddressChangedCallback;
                isWatching = false;
                PluginContext.Log(pluginName, "NetworkAddressChangeWatcher stopped.");
            }
        }

        void NetworkAddressChangedCallback(object sender, EventArgs e)
        {
            PluginContext.Log(pluginName, "Network address change detected, reviewing policy");
            //_ = WebPing.Instance.PrepareForConditionCheck(); //Will eventually determinecurrentpolicy
        }
    }
}

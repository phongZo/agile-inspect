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

        public void StartWatcher()
        {
            if (!isWatching)
            {
                NetworkChange.NetworkAddressChanged += NetworkAddressChangedCallback;
                isWatching = true;
                DebugLog.WriteLine("NetworkAddressChangeWatcher started.");
            }
        }

        public void StopWatcher()
        {
            if (isWatching)
            {
                NetworkChange.NetworkAddressChanged -= NetworkAddressChangedCallback;
                isWatching = false;
                DebugLog.WriteLine("NetworkAddressChangeWatcher stopped.");
            }
        }

        void NetworkAddressChangedCallback(object sender, EventArgs e)
        {
            DebugLog.WriteLine("Network address change detected, reviewing policy");
            //_ = WebPing.Instance.PrepareForConditionCheck(); //Will eventually determinecurrentpolicy
        }
    }
}

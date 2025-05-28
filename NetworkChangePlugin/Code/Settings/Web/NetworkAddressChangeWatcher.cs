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

        public void StartWatcher()
        {
            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(NetworkAddressChangedCallback);
        }

        void NetworkAddressChangedCallback(object sender, EventArgs e)
        {
            DebugLog.WriteLine("Network address change detected, reviewing policy");
            //_ = WebPing.Instance.PrepareForConditionCheck(); //Will eventually determinecurrentpolicy
        }
    }
}

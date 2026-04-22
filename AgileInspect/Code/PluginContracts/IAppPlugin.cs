using Newtonsoft.Json.Linq;

namespace AgileInspect.Code.PluginContracts
{
    public interface IAppCallback
    {
        void OnLog(string pluginName, string message);
        void OnDetectionResult(string pluginName, JToken jsonResult);

    }
    public interface IAppPlugin
    {
        string pluginName { get; }
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
    public interface IPrintScreenDetectorPlugin : IAppPlugin
    {
    }
    public interface IFocusWindowDetectorPlugin : IAppPlugin
    {
    }
    public interface IFirewallDetectorPlugin : IAppPlugin
    {
    }
    public interface IInternetDetectorPlugin : IAppPlugin
    {
    }
    public interface IVpnDetectorPlugin : IAppPlugin
    {
    }
    public interface IExternalDiskDetectorPlugin : IAppPlugin
    {
    }
    public interface IHostFileMonitorPlugin : IAppPlugin
    {
    }
    public interface IAiInteractionDetectorPlugin : IAppPlugin
    {
    }
    public interface IPrintDetectorPlugin : IAppPlugin
    {
    }
    public interface IClipboardMonitorPlugin : IAppPlugin
    {
    }
    public interface IScreenLockPlugin : IAppPlugin
    {
    }
    public interface IAutoUpdatePlugin : IAppPlugin
    {
    }
    public interface IUserPlugin : IAppPlugin
    {
    }
    public interface ISecureBootPlugin : IAppPlugin
    {
    }
    public interface IRdpPlugin : IAppPlugin
    {
    }
    public interface IDevModePlugin : IAppPlugin
    {
    }
    public interface IRemoteAccessToolsPlugin : IAppPlugin
    {
    }
    public interface ICloudSyncClientsPlugin : IAppPlugin
    {
    }
    public interface IPersonalMessagingAppsPlugin : IAppPlugin
    {
    }
    public interface IUsbDevicePlugin : IAppPlugin
    {
    }
    public interface IUnknownBluetoothPlugin : IAppPlugin
    {
    }
    public interface IScreenSharingPlugin : IAppPlugin
    {
    }
    public interface IVirtualMachinePlugin : IAppPlugin
    {
    }
}

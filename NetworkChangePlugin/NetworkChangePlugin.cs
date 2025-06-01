using AgileInspect.Code.PluginContracts;
using NetworkChangePlugin.Code.Settings.Web;
using System.Text.Json;

namespace NetworkChangePlugin
{
    public class NetworkChangePlugin : INetworkChangePlugin
    {
        public NetworkAddressChangeWatcher NetworkAddressChangeWatcher { get; set; } = new NetworkAddressChangeWatcher();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public string Name => "NetworkChangePlugin";

        public void SetCallback(IAppCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            PluginContext.SetCallback(callback);
        }

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
                : null;

            setting.EventParams = parsedEventParams ?? setting.EventParams;
            setting.TriggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.TriggerType;
            setting.TriggerParams = parsedTriggerParams ?? setting.TriggerParams;

            StoreCfgJson.Instance.EventSetting = setting;
            PluginContext.Log(Name, $"Parameters is set ");

        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;

            PluginContext.Log(Name, $"{Name} started.");
            PluginContext.Log(Name, $"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("RealTime", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run interval
            }
            else if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"{Name}: TriggerType 'Interval' is not implemented, fallback to RealTime.");
            }
            else
            {
                PluginContext.Log(Name, $"{Name}: Unknown TriggerType '{triggerType}, plugin will not start.");
                return;
            }
            NetworkAddressChangeWatcher.Instance.StartWatcher();

        }

        public void Stop()
        {
            PluginContext.Log(Name, $"Stopped.");
            NetworkAddressChangeWatcher.Instance.StopWatcher();
        }
    }
}

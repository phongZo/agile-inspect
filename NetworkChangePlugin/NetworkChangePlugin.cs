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

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }

        public void SetParameters(string eventParamsJson, string triggerType, string triggerParamsJson)
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();

            var parsedEventParams = !string.IsNullOrWhiteSpace(eventParamsJson)
                ? JsonSerializer.Deserialize<EventParams>(eventParamsJson)
                : null;

            var parsedTriggerParams = !string.IsNullOrWhiteSpace(triggerParamsJson)
                ? JsonSerializer.Deserialize<TriggerParams>(triggerParamsJson)
                : null;

            setting.eventParams = parsedEventParams ?? setting.eventParams;
            setting.triggerType = !string.IsNullOrWhiteSpace(triggerType) ? triggerType : setting.triggerType;
            setting.triggerParams = parsedTriggerParams ?? setting.triggerParams;

            StoreCfgJson.Instance.eventSetting = setting;
            PluginContext.Log(Name, $"Parameters is set ");

        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;

            PluginContext.Log(Name, $"{Name} started.");
            PluginContext.Log(Name, $"{Name}: triggerType : {triggerType}");

            if (triggerType.Equals("RealTime", StringComparison.OrdinalIgnoreCase))
            {
                // OK, run interval
            }
            else if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                PluginContext.Log(Name, $"{Name}: triggerType 'interval' is not implemented, fallback to RealTime.");
            }
            else
            {
                PluginContext.Log(Name, $"{Name}: Unknown triggerType '{triggerType}, plugin will not start.");
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

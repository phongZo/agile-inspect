using AgileInspect.Code;
using AgileInspect.Code.Settings;
using AgileInspect.Code.Plugins.PluginContracts;
using System.Threading.Tasks;
using System;

namespace AgileInspect
{
    internal class AgileInspectPlugin : IPlugin
    {
        private AsyncTimerService _eventQueueTimerService;
        public string Name => "AgileInspectPlugin";

        public void Execute()
        {
            Task.Run(async () =>
            {
                try
                {
                    await RunAsync();
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"[AgileInspectPlugin] Exception: {ex.Message}");
                }
            });
        }


        public async Task RunAsync()
        {
            DebugLog.Init();
            EventLog.Init();

            DebugLog.Write("", false);
            DebugLog.Write("--------AgileInspect Start-------");

            MachineName.Instance.UpdateName();
            StoreCfgLoader.Instance.Load();

            if (string.Equals(StoreCfgJson.Instance.deployType, "server", StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.Write("deployType is server - start HashCheckInterval.");
                await HashCheckInterval.Instance.GetHash();
                if (!HashCheckInterval.Instance.IsConfigUpdated)
                {
                    PluginManager.Instance.LoadPlugins();
                    PluginManager.Instance.StartAll();
                }
                HashCheckInterval.Instance.Start(skipImmediate: true);
            }
            else
            {
                DebugLog.Write("deployType is serverless - skip HashCheckInterval.");
                PluginManager.Instance.LoadPlugins();
                PluginManager.Instance.StartAll();
            }

            DebugLog.Write("[EventQueueService] Start EventQueueTimer");
            _eventQueueTimerService = new AsyncTimerService(StoreCfgJson.Instance.eventQueueConfig.interval * 1000, async () =>
            {
                await EventQueueService.Instance.SendQueueAsync();
            });
            _eventQueueTimerService.Start();
        }
    }
}

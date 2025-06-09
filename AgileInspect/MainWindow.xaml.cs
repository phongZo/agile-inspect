using AgileInspect.Code;
using AgileInspect.Code.Settings;
using System;
using System.Windows;

namespace AgileInspect
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; set; }
        private readonly PluginManager PluginManager = new();
        public MachineName MachineName { get; set; } = new MachineName();

        private RuleConditionQueueService RuleConditionQueueService { get; set; } = new RuleConditionQueueService();
        private EventQueueService EventQueueService { get; set; } = new EventQueueService();

        private AsyncTimerService _ruleConditionQueueTimerService;
        private AsyncTimerService _eventQueueTimerService;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Hide();
            Instance = this;

            DebugLog.Write("", false);
            DebugLog.Write("--------AgileInspect Start-------");
            MachineName.Instance.UpdateName();
            StoreCfgLoader.Load();
            if (string.Equals(StoreCfgJson.Instance.deployType, "server", StringComparison.OrdinalIgnoreCase))
            {
                DebugLog.Write("deployType is server - start HashCheckInterval.");
                HashCheckInterval.Instance.Start();
            }
            else
            {
                DebugLog.Write("deployType is serverless - skip HashCheckInterval.");

                PluginManager.Instance.LoadPlugins();
                PluginManager.Instance.StartAll();
            }
            /*
            // set timer
            _ruleConditionQueueTimerService = new AsyncTimerService(StoreCfgJson.Instance.ruleConditionQueueConfig.interval * 1000, async () =>
			{
				await RuleConditionQueueService.Instance.SendQueueAsync();
			});
            _ruleConditionQueueTimerService.Start();
			*/
            _eventQueueTimerService = new AsyncTimerService(StoreCfgJson.Instance.eventQueueConfig.interval * 1000, async () =>
            {
                await EventQueueService.Instance.SendQueueAsync();
            });
            _eventQueueTimerService.Start();

        }
    }
}

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

        private AsyncTimerService _timerService;
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
            StoreCfgLoader.Load();
            if (string.Equals(StoreCfgJson.Instance.DeployType, "Server", StringComparison.OrdinalIgnoreCase))
            {
                HashCheckInterval.Instance.Start();
            }
            else
            {
                DebugLog.Write("DeployType is serverless - skip HashCheckInterval.");
            }
            MachineName.Instance.UpdateName();
            string pluginsDir = AppDomain.CurrentDomain.BaseDirectory;
            PluginManager.LoadPlugins(pluginsDir);
            PluginManager.StartAll();

            // set timer
            _timerService = new AsyncTimerService(StoreCfgJson.Instance.RuleConditionQueueConfig.Interval * 1000, async () =>
            {
                await RuleConditionQueueService.Instance.SendQueueAsync();
            });
            _timerService.Start();

        }
    }
}

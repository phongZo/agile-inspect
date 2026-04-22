using AgileInspect.Code;
using AgileInspect.Code.Ipc;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
        public IpcService IpcService { get; set; } = new IpcService();
        private BackgroundTimer LogRotateTimer;
        public WindowProc WindowProc { get; set; } = new WindowProc();


        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowProc.Instance.Init(this);
            Hide();
            Instance = this;

            DebugLog.Write("", false);
            DebugLog.Write("--------AgileInspect Start-------");
            // set timer
            LogRotateTimer = new BackgroundTimer(LogRotateTimerCallback, "log rotation");
            //Setup timer call back for log rotate
            LogRotateTimer.StopIfRunning();
            //LogRotateTimer.Start(0); //start immediately
            //Prevent kill
            Unkillable.UnkillableInit();
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
            MachineName.Instance.UpdateName();
            StoreCfgLoader.Instance.Load();
            IpcService.Instance.startSendingProcess();
            RuleService.Load();
            await Task.Delay(3000);
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

                DebugLog.Write("[EventQueueService] Start EventQueueTimer");
                _eventQueueTimerService = new AsyncTimerService(StoreCfgJson.Instance.eventQueueConfig.interval * 1000, async () =>
                {
                    await EventQueueService.Instance.SendQueueAsync();
                    await BehaviorWatchService.Instance.SendSessionQueueAsync();
                });
                _eventQueueTimerService.Start();
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

            //DebugLog.Write("[EventQueueService] Start EventQueueTimer");
            //_eventQueueTimerService = new AsyncTimerService(StoreCfgJson.Instance.eventQueueConfig.interval * 1000, async () =>
            //{
            //    await EventQueueService.Instance.SendQueueAsync();
            //});
            //_eventQueueTimerService.Start();

            //Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            DebugLog.WriteLine("User switch " + e.Reason.ToString());
            DebugLog.WriteLine("User switch " + e.Reason.ToString());
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect
                || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteDisconnect
                || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogoff)
            {
                Application.Current.Shutdown();
            }
        }
        public void LogRotateTimerCallback()
        {
            try
            {
                // perform log rotation
                DebugLog.WriteLine("Log rotation interval hit");
                LogRotate.HandleRotation();

                //set 24h
                LogRotateTimer.Interval = new TimeSpan(24, 0, 0);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("Error in TimerCall: " + ex.Message);
            }
        }
    }
}

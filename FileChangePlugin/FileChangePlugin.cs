using AgileInspect.Code.PluginContracts;
namespace FileChangePlugin
{
    public class FileChangePlugin : IFileChangePlugin
    {
        public FileScanner FileScanner { get; set; } = new FileScanner();
        public FileWatcher FileWatcher { get; set; } = new FileWatcher();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public Permission Permission { get; set; } = new Permission();

        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write("Initializing FileChangePlugin...", false);
        }

        public void Start()
        {
            DebugLog.Write("", false);
            DebugLog.Write("Starting FileChangePlugin...", false);
            DebugLog.Write("---------------------------------", false);
            LogRotateTimerCallback();
            TimerWrapper LogRotateTimer = new TimerWrapper(LogRotateTimerCallback);
            LogRotateTimer.StopIfRunning();
            LogRotateTimer.Start(86400);

            FileScanTimerCallback();
            TimerWrapper ScheduledFileScanTimmer = new TimerWrapper(FileScanTimerCallback);
            ScheduledFileScanTimmer.StopIfRunning();
            ScheduledFileScanTimmer.Start(1800);
        }
        private void FileScanTimerCallback()
        {
            try
            {
                DebugLog.WriteLine("[FileScanner] Interval hit");
                FileScanner.Instance.StartHandleScan();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("Error in FileScanTimer: " + ex.Message);
            }
        }

        private void LogRotateTimerCallback()
        {
            try
            {
                DebugLog.WriteLine("[LogRotation] Interval hit");
                LogRotate.HandleRotation();
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("Error in LogRotateTimer: " + ex.Message);
            }
        }

        public void Stop()
        {
            DebugLog.Write("Stop FileChangePlugin...", false);
        }
    }
}

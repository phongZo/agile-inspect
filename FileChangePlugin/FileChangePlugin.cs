using AgileInspect.Code.PluginContracts;

namespace FileChangePlugin
{
    using System.Text.Json;
    using System.Threading;

    public class FileChangePlugin : IFileChangePlugin
    {
        public FileScanner FileScanner { get; set; } = new FileScanner();
        public FileWatcher FileWatcher { get; set; } = new FileWatcher();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public Permission Permission { get; set; } = new Permission();

        public string Name => "FileChangePlugin";

        private Timer ScanTimer;
        private Timer LogRotateTimer;

        public void Initialize()
        {
            DebugLog.Init();
            DebugLog.Write($"Initializing {Name}...", false);
        }

        public void Start()
        {
            DebugLog.Write("", false);
            DebugLog.Write($"Starting {Name}...", false);
            DebugLog.Write("---------------------------------", false);

            LogRotateTimer = new Timer(LogRotateTimerCallBack, null, 0, 24 * 60 * 60 * 1000); // 86400000 ms

            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var intervalSeconds = setting.TriggerParams.Interval;
            var scanDirs = setting.EventParams?.Paths;

            DebugLog.WriteLine($"{Name}: TriggerType : {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
            {
                ScanTimer = new Timer(FileScannerTimerCallBack, null, 0, intervalSeconds * 1000); // 86400000 ms
            }

            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                FileScanner.StartHandleScan();
                foreach (var dir in scanDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        DebugLog.WriteLine($"[FileWatcher] Start watching directory: {dir}");
                        FileWatcher.Instance.StartWatching(dir);
                    }
                    else
                    {
                        DebugLog.WriteLine($"[FileWatcher] Directory does not exist: {dir}");
                    }
                }
            }
            else
            {
                DebugLog.WriteLine($"[Trigger] Unsupported TriggerType '{triggerType}, plugin will not start.");
                return;
            }
        }


        public void Stop()
        {
            DebugLog.Write("Stop FileChangePlugin...", false);
            ScanTimer?.Dispose();
            ScanTimer = null;
            FileWatcher.Instance.StopWatching();
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
        }
        private void LogRotateTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            LogRotate.HandleRotation();
        }

        private void FileScannerTimerCallBack(object? state)
        {
            DebugLog.WriteLine("[LogRotate] Interval hit");
            FileScanner.Instance.StartHandleScan();        
        }   
    }

}


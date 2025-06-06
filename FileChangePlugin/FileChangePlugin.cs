using AgileInspect.Code.PluginContracts;

namespace FileChangePlugin
{
    using System.Text.Json;

    public class FileChangePlugin : IFileChangePlugin
    {
        public FileScanner FileScanner { get; set; } = new FileScanner();
        public FileWatcher FileWatcher { get; set; } = new FileWatcher();
        public StoreCfgJson StoreCfgJson { get; set; } = new StoreCfgJson();
        public Permission Permission { get; set; } = new Permission();

        public string Name => "FileChangePlugin";
        private AsyncTimerService _scanTimer;

        public void Initialize()
        {
            PluginContext.Log(Name, $"Initialize");
        }

        public void Start()
        {
            var setting = StoreCfgJson.Instance.eventSetting ?? new EventSetting();
            var triggerType = setting.triggerType;
            var intervalSeconds = setting.triggerParams.interval;
            var scanDirs = setting.eventParams?.paths;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"triggerType: {triggerType}");

            if (triggerType.Equals("interval", StringComparison.OrdinalIgnoreCase))
            {
                _scanTimer = new AsyncTimerService(intervalSeconds * 1000, FileScannerTimerCallback);
                _scanTimer.Start();
            }
            else if (triggerType.Equals("Realtime", StringComparison.OrdinalIgnoreCase))
            {
                FileScanner.Instance.HandleScan();
                foreach (var dir in scanDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        PluginContext.Log(Name, $"[FileWatcher] Start watching directory: {dir}");
                        FileWatcher.Instance.StartWatching(dir);
                    }
                    else
                    {
                        PluginContext.Log(Name, ($"[FileWatcher] Directory does not exist: {dir}"));
                    }
                }
            }
            else
            {
                PluginContext.Log(Name, $"[Trigger] Unsupported triggerType '{triggerType}, plugin will not start.");
                return;
            }
        }

        public void Stop()
        {
            PluginContext.Log(Name, "Stopped.");
            _scanTimer?.Stop();
            _scanTimer?.Dispose();
            _scanTimer = null;
            FileWatcher.Instance.StopWatching();
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

        private async Task FileScannerTimerCallback()
        {
            PluginContext.Log(Name, "[LogRotate] interval hit");
            try
            {
                await Task.Run(() => FileScanner.Instance.HandleScan());
            }
            catch (Exception ex)
            {
                PluginContext.Log(Name, $"[FileScanner] Error during scan: {ex.Message}");
            }
        }

    }

}


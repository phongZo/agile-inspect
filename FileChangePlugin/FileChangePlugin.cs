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

        public void Start()
        {
            var setting = StoreCfgJson.Instance.EventSetting ?? new EventSetting();
            var triggerType = setting.TriggerType;
            var intervalSeconds = setting.TriggerParams.Interval;
            var scanDirs = setting.EventParams?.Paths;

            PluginContext.Log(Name, "Start");
            PluginContext.Log(Name, $"TriggerType: {triggerType}");

            if (triggerType.Equals("Interval", StringComparison.OrdinalIgnoreCase))
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
                        PluginContext.Log(Name,$"[FileWatcher] Start watching directory: {dir}");
                        FileWatcher.Instance.StartWatching(dir);
                    }
                    else
                    {
                        PluginContext.Log(Name,($"[FileWatcher] Directory does not exist: {dir}"));
                    }
                }
            }
            else
            {
                PluginContext.Log(Name, $"[Trigger] Unsupported TriggerType '{triggerType}, plugin will not start.");
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

        private async Task FileScannerTimerCallback()
        {
            PluginContext.Log(Name, "[LogRotate] Interval hit");
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


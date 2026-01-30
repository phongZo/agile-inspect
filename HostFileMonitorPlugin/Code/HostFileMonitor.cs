using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace HostFileMonitorPlugin
{
    public class HostFileMonitor
    {
        #region Singleton
        public static HostFileMonitor Instance { get; set; }
        public HostFileMonitor()
        {
            Instance = this;
        }
        #endregion

        private FileSystemWatcher? EventWatcher;
        // C:\Windows\System32\drivers\etc
        private string HostsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "drivers", 
            "etc"
        );
        private const string HostsFile = "hosts";
        private string HostsPath => Path.Combine(HostsDir, HostsFile);

        private string? _lastChecksum;

        string pluginName = "HostFileMonitorPlugin";

        public void StartWatcher()
        {
            try
            {
                Checksum();
                EventWatcher = new FileSystemWatcher(HostsDir, HostsFile)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                EventWatcher.Changed += OnChanged;

                PluginContext.Log(pluginName, "[HostFileMonitor] Realtime watcher started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] Failed to start watcher: {ex.Message}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (EventWatcher != null)
                {
                    EventWatcher.EnableRaisingEvents = false;
                    EventWatcher.Changed -= OnChanged;
                    EventWatcher.Dispose();
                    EventWatcher = null;
                    PluginContext.Log(pluginName, "[HostFileMonitor] Realtime watcher stopped.");
                }
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] Error stopping realtime watcher: {ex.Message}");
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed) return;
            Checksum();
        }

        public void Checksum()
        {
            string newHash;
            try
            {
                newHash = ComputeSha256(HostsPath);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] Failed to read file: {ex.Message}");
                return;
            }

            if (string.Equals(_lastChecksum, newHash, StringComparison.Ordinal))
            {
                var resultObj = new JObject
                {
                    [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = false
                };
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
                PluginContext.SendDetectionResult(pluginName, resultObj);
                return;
            }

            if (string.IsNullOrEmpty(_lastChecksum))
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] current checksum = {newHash}");
            }
            else
            {
                PluginContext.Log(pluginName, $"[HostFileMonitor] file changed. Old checksum = {_lastChecksum} New checksum = {newHash}");
                // save last state and check rule

                var resultObj = new JObject
                {
                    [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = true
                };
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
                PluginContext.SendDetectionResult(pluginName, resultObj);
            }
            _lastChecksum = newHash;
        }

        private string ComputeSha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
    }
}

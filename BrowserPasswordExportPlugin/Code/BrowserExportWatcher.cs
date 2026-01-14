using System.Diagnostics;
using System.Windows.Automation;
using BrowserPasswordExportPlugin.Code.Settings;
using Newtonsoft.Json;

namespace BrowserPasswordExportPlugin.Code
{
    public class BrowserExportWatcher
    {
        private StoreCfgJson _config;
        private WindowEventListener _listener;
        private HashSet<IntPtr> _watchedWindows = new HashSet<IntPtr>();
        private const uint EVENT_OBJECT_DESTROY = 0x8001;

        public BrowserExportWatcher(StoreCfgJson config)
        {
            _config = config;
        }

        public void Start()
        {
            try
            {
                _listener = new WindowEventListener(OnWindowDetected);
                _listener.Start();
                PluginContext.Log("BrowserPasswordExportPlugin", "Watcher started (Simple Hook mode).");
            }
            catch (Exception ex)
            {
                PluginContext.Log("BrowserPasswordExportPlugin", $"Failed to start watcher: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _listener?.Stop();
                _listener?.Dispose();
                _listener = null;
                _watchedWindows.Clear();
                PluginContext.Log("BrowserPasswordExportPlugin", "Watcher stopped.");
            }
            catch {}
        }

        private void OnWindowDetected(IntPtr hwnd, uint eventType)
        {
            try
            {
                // Cleanup when window closes
                if (eventType == EVENT_OBJECT_DESTROY)
                {
                    if (_watchedWindows.Contains(hwnd)) _watchedWindows.Remove(hwnd);
                    return;
                }

                // Avoid processing the same window multiple times
                if (_watchedWindows.Contains(hwnd)) return;

                var info = WindowEventListener.GetWindowInfo(hwnd);
                int pid = (int)info.pid;
                string windowTitle = info.title;

                if (pid == 0) return;

                Process process = null;
                try { process = Process.GetProcessById(pid); } catch { return; }
                
                string procName = process.ProcessName.ToLower();

                if (_config.targetProcesses.Contains(procName))
                {
                    if (!string.IsNullOrEmpty(windowTitle))
                    {
                        bool titleMatch = _config.titleKeywords.Any(k => windowTitle.ToLower().Contains(k));
                        
                        bool isConfirmDialog = _config.confirmOverwriteKeywords.Any(k => windowTitle.ToLower().Contains(k));

                        if (titleMatch && !isConfirmDialog)
                        {
                            _watchedWindows.Add(hwnd);

                            var element = AutomationElement.FromHandle(hwnd);
                            if (element != null)
                            {
                                ScanAndLog(element, procName, windowTitle, pid);
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private void ScanAndLog(AutomationElement element, string procName, string windowTitle, int pid)
        {
            try 
            {
                // Try to find the filename in the Edit control
                string fileNameValue = "Unknown";
                try 
                {
                    var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                    var edits = element.FindAll(TreeScope.Descendants, editCondition);
                    
                    foreach (AutomationElement edit in edits)
                    {
                        string value = GetTextValue(edit);
                        if (!string.IsNullOrEmpty(value))
                        {
                            string lowerValue = value.ToLower();
                            bool keywordMatch = _config.fileNameKeywords.Any(k => lowerValue.Contains(k));
                            bool extMatch = lowerValue.EndsWith(_config.fileExtension);

                            if (keywordMatch && extMatch)
                            {
                                fileNameValue = value;
                                break;
                            }
                        }
                    }
                } 
                catch {}

                string alertMsg = $"Browser Export Detected: App='{procName}' Title='{windowTitle}' (PID: {pid})";
                PluginContext.Log("BrowserPasswordExportPlugin", alertMsg);

                var result = new
                {
                    eventType = "browser_export",
                    process = procName,
                    file = fileNameValue,
                    timestamp = DateTime.Now
                };
                PluginContext.SendDetectionResult("BrowserPasswordExportPlugin", JsonConvert.SerializeObject(result));
            }
            catch(Exception ex)
            {
                PluginContext.Log("BrowserPasswordExportPlugin", $"Scan Error: {ex.Message}");
            }
        }

        private string GetTextValue(AutomationElement element)
        {
            try
            {
                object patternObj;
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                {
                    var valuePattern = (ValuePattern)patternObj;
                    return valuePattern.Current.Value;
                }
                return element.Current.Name;
            }
            catch { return ""; }
        }
    }
}

using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using ClipboardMonitorPlugin.Code;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardMonitorPlugin
{
    public class ClipboardMonitor
    {
        // Win32 clipboard format order (first on stack = first enumerated)
        private const uint CF_TEXT = 1;
        private const uint CF_BITMAP = 2;
        private const uint CF_DIB = 8;
        private const uint CF_UNICODETEXT = 13;
        private const uint CF_HDROP = 15;

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();
        [DllImport("user32.dll")]
        private static extern uint EnumClipboardFormats(uint format);

        /// <summary>Returns first format on clipboard stack that is files, image, or text; else null.</summary>
        private static string? GetFirstSupportedFormat()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                uint format = 0;
                while ((format = EnumClipboardFormats(format)) != 0)
                {
                    if (format == CF_HDROP) return "files";
                    if (format == CF_BITMAP || format == CF_DIB) return "image";
                    if (format == CF_TEXT || format == CF_UNICODETEXT) return "text";
                }
                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }

        #region Singleton
        public static ClipboardMonitor Instance { get; set; }
        public ClipboardMonitor()
        {
            Instance = this;
        }
        #endregion

        private ClipboardWindow? _window;
        private Dispatcher? _watcherDispatcher;
        private Thread? _watcherThread;
        private readonly ManualResetEventSlim _watcherReady = new(false);

        string pluginName = "ClipboardMonitorPlugin";

        public void StartWatcher()
        {
            try
            {
                if (_window != null || (_watcherThread?.IsAlive ?? false)) return;

                _watcherReady.Reset();
                Exception? startupEx = null;
                _watcherThread = new Thread(() =>
                {
                    try
                    {
                        _watcherDispatcher = Dispatcher.CurrentDispatcher;
                        _window = new ClipboardWindow(() =>
                        {
                            string type = "unknown";
                            string contentPreview = "";
                            bool? purviewIsLabeled = null;
                            bool? purviewIsRmsProtected = null;
                            string? purviewMainLabelName = null;

                            // First item on clipboard stack only (Win32 format order)
                            string? firstFormat = GetFirstSupportedFormat();
                            if (firstFormat == "files")
                            {
                                type = "files";
                                try
                                {
                                    var files = Clipboard.GetFileDropList();
                                    contentPreview = $"Files count: {files.Count}";
                                    if (files.Count > 0)
                                    {
                                        var firstPath = files[0];
                                        if (!string.IsNullOrEmpty(firstPath))
                                        {
                                            var st = PurviewGetFileStatus.Query(firstPath);
                                            if (st.Ok)
                                            {
                                                purviewIsLabeled = st.IsLabeled;
                                                purviewIsRmsProtected = st.IsRMSProtected;
                                                purviewMainLabelName = st.MainLabelName;
                                            }
                                            else if (!string.IsNullOrWhiteSpace(st.Error))
                                            {
                                                PluginContext.Log(pluginName, $"[ClipboardMonitor] Purview Get-FileStatus failed: {st.Error}");
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                            else if (firstFormat == "image")
                            {
                                type = "image";
                                contentPreview = "Image copied";
                            }
                            else if (firstFormat == "text")
                            {
                                type = "text";
                                try
                                {
                                    var text = Clipboard.GetText();
                                    if (text.Length > 100)
                                        text = text.Substring(0, 100);
                                    contentPreview = text.Replace("\r", " ").Replace("\n", " ");
                                }
                                catch { }
                            }

                            var payload = new JObject
                            {
                                ["event"] = "CLIPBOARD_UPDATED",
                                ["type"] = type,
                                ["preview"] = contentPreview,
                                // Always include Purview fields (empty when not applicable/unavailable)
                                ["IsLabeled"] = "",
                                ["IsRMSProtected"] = "",
                                ["MainLabelName"] = ""
                            };
                            if (type == "files")
                            {
                                payload["IsLabeled"] = purviewIsLabeled.HasValue ? purviewIsLabeled.Value : "";
                                payload["IsRMSProtected"] = purviewIsRmsProtected.HasValue ? purviewIsRmsProtected.Value : "";
                                payload["MainLabelName"] = purviewMainLabelName ?? "";
                            }
                            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), payload);
                            PluginContext.Log(pluginName, $"[ClipboardMonitor] type={type} preview={contentPreview}");
                            PluginContext.SendDetectionResult(pluginName, payload);
                        });

                        _watcherReady.Set();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        startupEx = ex;
                        _watcherReady.Set();
                    }
                });
                _watcherThread.SetApartmentState(ApartmentState.STA);
                _watcherThread.IsBackground = true;
                _watcherThread.Start();

                if (!_watcherReady.Wait(TimeSpan.FromSeconds(5)))
                {
                    PluginContext.Log(pluginName, "[ClipboardMonitor] StartWatcher timeout on STA watcher thread.");
                    return;
                }
                if (startupEx != null) throw startupEx;

                PluginContext.Log(pluginName, "[ClipboardMonitor] WM_CLIPBOARDUPDATE started (STA watcher thread).");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] StartWatcher error: {ex}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (_window == null) return;

                if (_watcherDispatcher == null)
                {
                    _window.Dispose();
                    _window = null;
                    _watcherThread = null;
                    return;
                }

                _watcherDispatcher.Invoke(() =>
                {
                    _window.Dispose();
                    _window = null;
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                });

                if (_watcherThread != null && _watcherThread.IsAlive)
                {
                    _watcherThread.Join(3000);
                }

                _watcherThread = null;
                _watcherDispatcher = null;
                PluginContext.Log(pluginName, "[ClipboardMonitor] WM_CLIPBOARDUPDATE stopped.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] StopWatcher error: {ex.Message}");
            }
        }
    }
}

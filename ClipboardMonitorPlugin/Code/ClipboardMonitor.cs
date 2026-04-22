using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using ClipboardMonitorPlugin.Code;
using ClipboardMonitorPlugin.Code.MIP;
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
        private MIPHelper? _mipHelper;
        private uint _lastSequenceNumber = 0;
        private Dispatcher? _watcherDispatcher;
        private Thread? _watcherThread;
        private readonly ManualResetEventSlim _watcherReady = new(false);

        string pluginName = "ClipboardMonitorPlugin";

        public void StartWatcher()
        {
            try
            {
                if (_window != null || (_watcherThread?.IsAlive ?? false)) return;

                _ = InitializeMIP();

                _watcherReady.Reset();
                Exception? startupEx = null;
                _watcherThread = new Thread(() =>
                {
                    try
                    {
                        _watcherDispatcher = Dispatcher.CurrentDispatcher;
                        _window = new ClipboardWindow(() =>
                        {
                            _ = ProcessClipboardFiles();
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

        private async Task InitializeMIP()
        {
            if (_mipHelper != null) return;

            var setting = StoreCfgJson.Instance.eventSetting;
            if (setting == null || setting.eventParams == null) return;

            var eventParams = setting.eventParams;
            _mipHelper = new MIPHelper(eventParams.mipClientId, eventParams.mipTenantId, eventParams.mipClientSecret);
            
            await _mipHelper.InitializeAsync();
        }

        public async Task ProcessClipboardFiles()
        {
            await InitializeMIP();

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                var tcs = new TaskCompletionSource<bool>();
                var thread = new Thread(() =>
                {
                    try
                    {
                        InternalProcessAsync().Wait();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex) { tcs.SetException(ex); }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                await tcs.Task;
                return;
            }

            await InternalProcessAsync();
        }

        private async Task InternalProcessAsync()
        {
            try
            {
                uint currentSequence = GetClipboardSequenceNumber();
                if (currentSequence == _lastSequenceNumber) return;
                _lastSequenceNumber = currentSequence;

                var types = new List<string>();
                string? contentPreview = null;
                var mipResults = new JArray();
                
                // Fields for backward compatibility with dev branch payload
                bool? purviewIsLabeled = null;
                bool? purviewIsRmsProtected = null;
                string? purviewMainLabelName = null;

                // 1. File & MIP Detection
                if (Clipboard.ContainsFileDropList())
                {
                    types.Add("files");
                    try
                    {
                        var files = Clipboard.GetFileDropList();
                        contentPreview = $"Files count: {files.Count}";

                        if (files.Count > 0)
                        {
                            var firstPath = files[0];
                            if (!string.IsNullOrEmpty(firstPath))
                            {
                                // Call Purview PS for the first file (from dev branch)
                                var st = PurviewGetFileStatus.Query(firstPath);
                                if (st.Ok)
                                {
                                    purviewIsLabeled = st.IsLabeled;
                                    purviewIsRmsProtected = st.IsRMSProtected;
                                    purviewMainLabelName = st.MainLabelName;
                                }
                            }
                        }

                        if (_mipHelper != null)
                        {
                            foreach (string filePath in files)
                            {
                                if (!File.Exists(filePath)) continue;
                                var result = await _mipHelper.GetLabelFromFileAsync(filePath);
                                if (result != null && result.HasLabel)
                                {
                                    mipResults.Add(new JObject
                                    {
                                        ["fileName"] = Path.GetFileName(filePath),
                                        ["labelName"] = result.Name,
                                        ["labelId"] = result.Id,
                                        ["method"] = result.DetectionMethod
                                    });

                                    PluginContext.Log(pluginName, $"[DETECTION] File: {Path.GetFileName(filePath)} | Label: {result.Name} ({result.Id}) | Method: {result.DetectionMethod}");
                                    
                                    // If we got info from MIP SDK for the first file, use it to populate purview fields too
                                    if (filePath == files[0])
                                    {
                                        purviewIsLabeled = true;
                                        purviewIsRmsProtected = result.IsProtected;
                                        purviewMainLabelName = result.Name;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Other Formats (Image, HTML, RTF, Text)
                if (Clipboard.ContainsImage()) { types.Add("image"); contentPreview ??= "Image copied"; }
                
                if (Clipboard.ContainsText(TextDataFormat.Html)) {
                    types.Add("html");
                    try { contentPreview ??= $"HTML length: {Clipboard.GetText(TextDataFormat.Html).Length}"; } catch { }
                }

                if (Clipboard.ContainsText(TextDataFormat.Rtf)) {
                    types.Add("rtf");
                    try { contentPreview ??= $"RTF length: {Clipboard.GetText(TextDataFormat.Rtf).Length}"; } catch { }
                }

                if (Clipboard.ContainsText()) {
                    types.Add("text");
                    try { 
                        var text = Clipboard.GetText();
                        contentPreview ??= (text.Length > 100 ? text.Substring(0, 100) : text).Replace("\r", " ").Replace("\n", " ");
                    } catch { }
                }

                // Guard clause: Exit if nothing detected
                if (!types.Any() && !mipResults.Any()) return;

                // 3. Build Payload & Send Result
                var payload = new JObject
                {
                    ["event"] = "CLIPBOARD_UPDATED",
                    ["type"] = types.FirstOrDefault() ?? "unknown", // Backward compatibility
                    ["types"] = new JArray(types.Any() ? types : new[] { "unknown" }),
                    ["preview"] = contentPreview ?? "",
                    
                    // Always include Purview fields (empty when not applicable/unavailable)
                    ["IsLabeled"] = purviewIsLabeled.HasValue ? purviewIsLabeled.Value.ToString() : "",
                    ["IsRMSProtected"] = purviewIsRmsProtected.HasValue ? purviewIsRmsProtected.Value.ToString() : "",
                    ["MainLabelName"] = purviewMainLabelName ?? ""
                };

                if (mipResults.Any()) payload["mip_labels"] = mipResults;

                var resultObj = new JObject { [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = payload };
                
                PluginContext.Log(pluginName, $"[ClipboardMonitor] types=[{string.Join(",", types)}] preview={contentPreview}");
                RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
                PluginContext.SendDetectionResult(pluginName, resultObj);
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"Internal Process Error: {ex.Message}");
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

        #region Win32 API
        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();
        #endregion
    }
}

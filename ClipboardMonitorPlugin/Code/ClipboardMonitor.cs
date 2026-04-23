#nullable enable
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
                
                // 1. File & MIP Detection
                if (Clipboard.ContainsFileDropList())
                {
                    types.Add("files");
                    try
                    {
                        var files = Clipboard.GetFileDropList();
                        contentPreview = $"Files count: {files.Count}";

                        foreach (string filePath in files)
                        {
                            if (!File.Exists(filePath)) continue;
                            
                            bool fileHasLabel = false;
                            bool fileHasProtection = false;
                            string? labelName = null;

                            if (_mipHelper != null)
                            {
                                var result = await _mipHelper.GetLabelFromFileAsync(filePath);
                                if (result != null)
                                {
                                    fileHasLabel = result.HasLabel;
                                    fileHasProtection = result.IsProtected;
                                    labelName = result.Name;

                                    if (fileHasLabel || fileHasProtection)
                                    {
                                        string detail = fileHasLabel ? $"Label: {labelName}" : "PROTECTED";
                                        if (fileHasLabel && fileHasProtection) detail += " [PROTECTED]";
                                        
                                        PluginContext.Log(pluginName, $"[DETECTION] File: {Path.GetFileName(filePath)} | {detail} | Method: {result.DetectionMethod}");
                                    }
                                }
                            }

                            var filePayload = new ClipboardPayload
                            {
                                type = "files",
                                preview = Path.GetFileName(filePath),
                                IsLabeled = fileHasLabel,
                                IsRMSProtected = fileHasProtection,
                                MainLabelName = labelName
                            };
                            
                            SendAndLogPayload(JObject.FromObject(filePayload));
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
                if (!types.Any()) return;

                // 3. Log Summary & Send
#if DEBUG
                PluginContext.Log(pluginName, $"[ClipboardMonitor] types=[{string.Join(",", types)}] preview={contentPreview}");
#else
                PluginContext.Log(pluginName, "[ClipboardMonitor] Clipboard updated.");
#endif

                if (types.Any(t => t != "files"))
                {
                    var otherPayload = new ClipboardPayload
                    {
                        type = types.FirstOrDefault(t => t != "files") ?? "unknown",
                        preview = contentPreview ?? ""
                    };
                    SendAndLogPayload(JObject.FromObject(otherPayload));
                }
            }
            catch (Exception ex)
            {

                PluginContext.Log(pluginName, $"Internal Process Error: {ex.Message}");
            }
        }

        private void SendAndLogPayload(JObject payload)
        {
            var resultObj = new JObject { [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = payload };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), payload);
            PluginContext.SendDetectionResult(pluginName, resultObj);
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

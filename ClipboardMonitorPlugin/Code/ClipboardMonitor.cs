using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using ClipboardMonitorPlugin.Code;
using ClipboardMonitorPlugin.Code.MIP;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace ClipboardMonitorPlugin
{
    public class ClipboardMonitor
    {
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

        string pluginName = "ClipboardMonitorPlugin";

        public void StartWatcher()
        {
            try
            {
                if (_window != null) return;

                _ = InitializeMIP();

                var thread = new Thread(() =>
                {
                    try
                    {
                        _window = new ClipboardWindow(async () =>
                        {
                            await ProcessClipboardFiles();
                        });
                        System.Windows.Threading.Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log(pluginName, $"[ClipboardMonitor] Window Thread Error: {ex.Message}");
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();

                PluginContext.Log(pluginName, "[ClipboardMonitor] WM_CLIPBOARDUPDATE started.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] StartWatcher Error: {ex.Message}");
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

                // Guard clause: Exit if nothing significant detected
                if (!types.Any() && !mipResults.Any()) return;

                // 3. Build Payload & Send Result
                var payload = new JObject
                {
                    ["event"] = "CLIPBOARD_UPDATED",
                    ["types"] = new JArray(types.Any() ? types : new[] { "unknown" }),
                    ["preview"] = contentPreview ?? ""
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
                _window.Dispose();
                _window = null;
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

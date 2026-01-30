using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using ClipboardMonitorPlugin.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Xml.Linq;

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

        string pluginName = "ClipboardMonitorPlugin";

        public void StartWatcher()
        {
            try
            {
                if (_window != null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _window = new ClipboardWindow(() =>
                    {
                        var types = new List<string>();
                        string? contentPreview = null;

                        // 1. File
                        if (Clipboard.ContainsFileDropList())
                        {
                            types.Add("files");
                            try
                            {
                                var files = Clipboard.GetFileDropList();
                                contentPreview = $"Files count: {files.Count}";
                            }
                            catch { }
                        }

                        // 2. Image
                        if (Clipboard.ContainsImage())
                        {
                            types.Add("image");
                            contentPreview ??= "Image copied";
                        }

                        // 3. HTML
                        if (Clipboard.ContainsText(TextDataFormat.Html))
                        {
                            types.Add("html");
                            try
                            {
                                var html = Clipboard.GetText(TextDataFormat.Html);
                                contentPreview ??= $"HTML length: {html.Length}";
                            }
                            catch { }
                        }

                        // 4. RTF
                        if (Clipboard.ContainsText(TextDataFormat.Rtf))
                        {
                            types.Add("rtf");
                            try
                            {
                                var rtf = Clipboard.GetText(TextDataFormat.Rtf);
                                contentPreview ??= $"RTF length: {rtf.Length}";
                            }
                            catch { }
                        }

                        // 5. Plain text
                        if (Clipboard.ContainsText())
                        {
                            types.Add("text");
                            try
                            {
                                var text = Clipboard.GetText();
                                if (text.Length > 100)
                                    text = text.Substring(0, 100);

                                contentPreview ??= text.Replace("\r", " ").Replace("\n", " ");
                            }
                            catch { }
                        }

                        types = (types.Count > 0) ? types : new List<string> { "unknown" };
                        contentPreview = contentPreview ?? "";

                        var payload = new JObject
                        {
                            ["event"] = "CLIPBOARD_UPDATED",
                            ["types"] = JArray.FromObject(types),
                            ["preview"] = contentPreview
                        };

                        var resultObj = new JObject
                        {
                            [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = payload
                        };
  
                        RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
                        PluginContext.Log(pluginName, $"[ClipboardMonitor] types=[{string.Join(",", types)}] preview={contentPreview}");

                        PluginContext.SendDetectionResult(pluginName, resultObj);
                    });
                });

                PluginContext.Log(pluginName, "[ClipboardMonitor] WM_CLIPBOARDUPDATE started (WPF HwndSource).");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] StartWatcher error: {ex.Message}");
            }
        }

        public void StopWatcher()
        {
            try
            {
                if (_window == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _window.Dispose();
                    _window = null;
                });

                PluginContext.Log(pluginName, "[ClipboardMonitor] WM_CLIPBOARDUPDATE stopped.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"[ClipboardMonitor] StopWatcher error: {ex.Message}");
            }
        }
    }
}

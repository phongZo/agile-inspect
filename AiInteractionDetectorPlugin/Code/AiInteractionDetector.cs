#nullable enable
using AgileInspect.Code.PluginContracts;
using AiInteractionDetectorPlugin.Code.Settings;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace AiInteractionDetectorPlugin
{
    public class AiSession
    {
        public string Keyword { get; set; } = "";
        public string SourceApps { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime LastSeen { get; set; }
        public string SessionKey => Keyword.ToLower(); 
        
        // Properties for logging details
        public int Pid { get; set; } // Process ID
        public string ProcessName { get; set; } = "";
        public string Details { get; set; } = "";
        public string DetectionType { get; set; } = ""; // "Web" or "Process"
    }

    public class AiInteractionDetector
    {
        public static AiInteractionDetector Instance { get; } = new AiInteractionDetector();
        private AiInteractionDetector() { }

        private AutomationFocusChangedEventHandler? _focusHandler;
        private ManagementEventWatcher? _startWatcher;
        private ManagementEventWatcher? _stopWatcher;
        
        private ConcurrentDictionary<string, AiSession> _activeSessions = new ConcurrentDictionary<string, AiSession>();
        private readonly object _scanLock = new object();
        private CancellationTokenSource? _activeTrackingCts;
        
        // Timeout is fallback, we try to detect stop events actively now
        private const int SessionTimeoutSeconds = 15;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public void StartWatcher()
        {
            var setting = StoreCfgJson.Instance.eventSetting;
            if (setting.eventParams.aiDomains != null)
            {
                PluginContext.Log("AiInteractionDetectorPlugin", $"Monitoring AI Domains: {string.Join(", ", setting.eventParams.aiDomains)}");
            }
            if (setting.eventParams.browsers != null)
            {
                PluginContext.Log("AiInteractionDetectorPlugin", $"Monitoring Browsers: {string.Join(", ", setting.eventParams.browsers)}");
            }

            StopWatcher(); 
            StartUIWatcher();
            StartProcessEventWatchers(); 
            
            Task.Run(async () => 
            {
                await Task.Delay(500); 
                PluginContext.Log("AiInteractionDetectorPlugin", "Performing initial system scan...");
                TriggerScan("Initial");
            });
        }

        public void StopWatcher()
        {
            StopActiveTracking();
            if (_focusHandler != null) { try { Automation.RemoveAutomationFocusChangedEventHandler(_focusHandler); } catch { } _focusHandler = null; }
            if (_startWatcher != null) { try { _startWatcher.Stop(); _startWatcher.Dispose(); } catch { } _startWatcher = null; }
            if (_stopWatcher != null) { try { _stopWatcher.Stop(); _stopWatcher.Dispose(); } catch { } _stopWatcher = null; }
            _activeSessions.Clear();
        }

        private void StartUIWatcher()
        {
            try
            {
                _focusHandler = new AutomationFocusChangedEventHandler(OnFocusChanged);
                Automation.AddAutomationFocusChangedEventHandler(_focusHandler);
            }
            catch { }
        }

        private void StartProcessEventWatchers()
        {
            try
            {
                var startQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
                _startWatcher = new ManagementEventWatcher(startQuery);
                _startWatcher.EventArrived += (s, e) => TriggerScan("ProcessStart");
                _startWatcher.Start();

                var stopQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
                _stopWatcher = new ManagementEventWatcher(stopQuery);
                _stopWatcher.EventArrived += (s, e) => TriggerScan("ProcessStop");
                _stopWatcher.Start();
            }
            catch { }
        }

        private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
        {
            TriggerScan("FocusChange");
        }

        private void TriggerScan(string reason)
        {
            lock (_scanLock)
            {
                PerformScanCycle();
                ManageActiveTracking();
            }
        }

        private void ManageActiveTracking()
        {
            if (!_activeSessions.IsEmpty && _activeTrackingCts == null)
            {
                _activeTrackingCts = new CancellationTokenSource();
                Task.Run(async () => 
                {
                    while (_activeTrackingCts != null && !_activeTrackingCts.Token.IsCancellationRequested)
                    {
                        if (_activeSessions.IsEmpty) break;
                        lock (_scanLock) { PerformScanCycle(); }
                        await Task.Delay(1000, _activeTrackingCts.Token);
                    }
                    _activeTrackingCts = null;
                });
            }
            else if (_activeSessions.IsEmpty && _activeTrackingCts != null)
            {
                StopActiveTracking();
            }
        }

        private void StopActiveTracking()
        {
            if (_activeTrackingCts != null)
            {
                _activeTrackingCts.Cancel();
                _activeTrackingCts = null;
            }
        }

        private void PerformScanCycle()
        {
            var processes = Process.GetProcesses();
            var detectedSessions = new List<AiSession>();
            var domains = StoreCfgJson.Instance.eventSetting.eventParams.aiDomains;
            
            // Blocklist for generic keywords that might be extracted from subdomains
            var noiseKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
                "api", "www", "app", "proxy", "auth", "login", "account", "secure", "mail", "remote", "server" 
            };

            // 1. Process Name Scan
            if (domains != null)
            {
                foreach (var p in processes)
                {
                    try 
                    {
                        string pName = p.ProcessName;
                        if (IsBrowser(pName)) continue;

                        foreach (var key in domains)
                        {
                            // Smart Clean: "api.githubcopilot.com" -> "githubcopilot"
                            string cleanKey = GetMainDomainKeyword(key);
                            
                            if (cleanKey.Length < 3 || noiseKeywords.Contains(cleanKey)) continue;

                            if (pName.IndexOf(cleanKey, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                 detectedSessions.Add(new AiSession {
                                     Keyword = cleanKey,
                                     ProcessName = pName,
                                     Pid = p.Id,
                                     DetectionType = "Process",
                                     Details = "Standalone App",
                                     StartTime = DateTime.Now,
                                     LastSeen = DateTime.Now
                                 });
                                 break; 
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Browser Scan
            var browserSession = ScanBrowserFocus();
            if (browserSession != null) detectedSessions.Add(browserSession);

            // 3. Update Sessions (Start new ones, Update existing)
            DateTime now = DateTime.Now;

            foreach (var session in detectedSessions)
            {
                string key = $"{session.Keyword.ToLower()}:{session.ProcessName.ToLower()}";

                _activeSessions.AddOrUpdate(key, 
                    k => {
                        session.StartTime = now;
                        session.LastSeen = now;
                        // Format: [STARTED] AI: claude | Source: chrome (PID: 1234) | Info: ...
                        string pidInfo = session.Pid > 0 ? $" (PID: {session.Pid})" : "";
                        string info = !string.IsNullOrEmpty(session.Details) ? $" | Info: {session.Details}" : "";
                        PluginContext.Log("AiInteractionDetectorPlugin", $"[STARTED] AI: {session.Keyword} | Type: {session.DetectionType} | Source: {session.ProcessName}{pidInfo}{info}");

                        try
                        {
                            var resultObj = new JObject
                            {
                                ["isOn"] = true,
                                ["keyword"] = session.Keyword,
                                ["process"] = session.ProcessName,
                                ["type"] = session.DetectionType
                            };
                            PluginContext.SendDetectionResult("AiInteractionDetectorPlugin", resultObj);
                        }
                        catch { }

                        return session;
                    },
                    (k, s) => {
                        s.LastSeen = now;
                        s.Pid = session.Pid > 0 ? session.Pid : s.Pid; // Update PID if changed
                        if (!string.IsNullOrEmpty(session.Details)) s.Details = session.Details;
                        return s;
                    });
            }

            // 4. STOP LOGIC (Focus Loss & Process Termination)
            var keys = _activeSessions.Keys.ToList();
            foreach (var key in keys)
            {
                if (_activeSessions.TryGetValue(key, out var session))
                {
                    bool shouldStop = false;
                    string stopReason = "Timeout"; // Default reason

                    // Strategy A: Web Sessions -> Stop if Focus Lost
                    // If this session is Web, and the CURRENT detected browser session is NOT this one, then we lost focus.
                    if (session.DetectionType == "Web")
                    {
                        // If browserSession is null (no browser focused) OR browserSession is different from this session
                        if (browserSession == null || 
                            !string.Equals(browserSession.Keyword, session.Keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldStop = true;
                            stopReason = "Focus Lost (Tab/Window Changed)";
                        }
                    }
                    // Strategy B: App Sessions -> Stop if Process Dead
                    else if (session.DetectionType == "Process")
                    {
                        try 
                        {
                            Process.GetProcessById(session.Pid); // Will throw if dead
                        }
                        catch 
                        {
                            shouldStop = true;
                            stopReason = "Process Terminated";
                        }
                    }

                    // Strategy C: Timeout Fallback (just in case active tracking missed it)
                    if (!shouldStop && (now - session.LastSeen).TotalSeconds > SessionTimeoutSeconds)
                    {
                        shouldStop = true;
                        stopReason = "Session Timeout";
                    }

                    if (shouldStop)
                    {
                        if (_activeSessions.TryRemove(key, out _))
                        {
                            PluginContext.Log("AiInteractionDetectorPlugin", $"[STOPPED] AI: {session.Keyword} | Type: {session.DetectionType} | Reason: {stopReason}");

                            try
                            {
                                var resultObj = new JObject
                                {
                                    ["isOn"] = false,
                                    ["keyword"] = session.Keyword,
                                    ["process"] = session.ProcessName,
                                    ["type"] = session.DetectionType
                                };
                                PluginContext.SendDetectionResult("AiInteractionDetectorPlugin", resultObj);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private string GetMainDomainKeyword(string domain)
        {
            // example: Extract "githubcopilot" from "api.githubcopilot.com"
            if (!domain.Contains(".")) return domain;
            
            var parts = domain.Split('.');
            if (parts.Length == 2) return parts[0];
            if (parts.Length >= 2) return parts[parts.Length - 2];
            
            return domain; 
        }

        private AiSession? ScanBrowserFocus()
        {
            var domains = StoreCfgJson.Instance.eventSetting.eventParams.aiDomains;
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;

            try
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                var proc = Process.GetProcessById((int)pid);
                if (!IsBrowser(proc.ProcessName)) return null;

                string url = GetBrowserUrl(hWnd);
                if (!string.IsNullOrEmpty(url))
                {
                    string currentHost = url.Trim();
                    try
                    {
                        string tempUrl = url.Contains("://") ? url : "https://" + url;
                        var uri = new Uri(tempUrl);
                        currentHost = uri.Host;
                    }
                    catch { }

                    var detected = domains.FirstOrDefault(k => 
                        currentHost.Equals(k, StringComparison.OrdinalIgnoreCase) || 
                        currentHost.EndsWith("." + k, StringComparison.OrdinalIgnoreCase)
                    );

                    if (detected != null)
                    {
                        return new AiSession
                        {
                            Keyword = currentHost, 
                            ProcessName = proc.ProcessName,
                            Pid = (int)pid,
                            DetectionType = "Web",
                            Details = url,
                            StartTime = DateTime.Now,
                            LastSeen = DateTime.Now
                        };
                    }
                }
            }
            catch {}
            return null;
        }

        private bool IsBrowser(string processName)
        {
            var configuredBrowsers = StoreCfgJson.Instance.eventSetting.eventParams.browsers;
            if (configuredBrowsers != null && configuredBrowsers.Length > 0)
                return configuredBrowsers.Any(b => processName.Contains(b, StringComparison.OrdinalIgnoreCase));
            return false;
        }

        private string GetBrowserUrl(IntPtr hWnd)
        {
            try
            {
                AutomationElement element = AutomationElement.FromHandle(hWnd);
                if (element == null) return "";
                var addressBar = element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"));
                if (addressBar == null)
                {
                    var edits = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    foreach (AutomationElement edit in edits)
                    {
                        try {
                            if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj))
                            {
                                string val = ((ValuePattern)patternObj).Current.Value;
                                if (val.Contains(".") && !val.Contains(" ")) return val;
                            }
                        } catch {}
                    }
                }
                if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    return ((ValuePattern)pattern).Current.Value;
                }
            }
            catch { }
            return "";
        }
    }
}
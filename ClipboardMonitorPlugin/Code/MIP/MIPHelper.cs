using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.Loader;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.File;

namespace ClipboardMonitorPlugin.Code.MIP
{
    public class LabelResult
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Owner { get; set; }
        public string? TenantId { get; set; }
        public bool IsProtected { get; set; }
        public bool HasLabel => !string.IsNullOrEmpty(Id);
        public string DetectionMethod { get; set; }
        public DateTime LastScannedAt { get; set; }
    }

    public class MIPHelper
    {
        #region Constants
        public const string METHOD_SDK = "SDK";
        public const string METHOD_OFFLINE_UNZIP = "OFFLINE_UNZIP";
        public const string METHOD_OFFLINE_BINARY = "OFFLINE_BINARY";
        public const string METHOD_NOT_FOUND = "NONE";
        #endregion

        private readonly string pluginName = "ClipboardMonitorPlugin";
        private ConcurrentDictionary<string, string> _labelNameCache = new ConcurrentDictionary<string, string>();
        private readonly string _labelMapPath;
        private bool _sdkInitialized = false;

        private MipContext _mipContext;
        private IFileProfile _fileProfile;
        private IFileEngine _fileEngine;

        private string _clientId;
        private string _tenantId;
        private string _clientSecret;

        public MIPHelper(string clientId = null, string tenantId = null, string clientSecret = null)
        {
            _clientId = clientId;
            _tenantId = tenantId;
            _clientSecret = clientSecret;

            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgileInspect");
            if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
            _labelMapPath = Path.Combine(appDataFolder, "labels_map.json");

            LoadLabelMap();
            NetworkChange.NetworkAddressChanged += (s, e) => { if (!_sdkInitialized) _ = InitializeAsync(); };
        }

        private void LoadLabelMap()
        {
            try {
                if (File.Exists(_labelMapPath)) {
                    var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_labelMapPath));
                    if (map != null) {
                        _labelNameCache = new ConcurrentDictionary<string, string>(
                            map.ToDictionary(k => k.Key.ToLower(), v => v.Value)
                        );
                    }
                }
            } catch { }
        }

        private void SaveLabelMap()
        {
            try {
                File.WriteAllText(_labelMapPath, JsonConvert.SerializeObject(_labelNameCache, Formatting.Indented));
            } catch { }
        }

        public async Task InitializeAsync()
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_tenantId) || string.IsNullOrEmpty(_clientSecret))
            {
                PluginContext.Log(pluginName, "MIP Credentials missing. SDK will run in Offline mode only.");
                return;
            }

            try {
                await InitSDKInternalAsync();
            } catch (Exception ex) {
                PluginContext.Log(pluginName, $"SDK Init Failed: {ex.Message}");
            }
        }

        private async Task InitSDKInternalAsync()
        {
            try 
            {
                Microsoft.InformationProtection.MIP.Initialize(MipComponent.File);

                ApplicationInfo appInfo = new ApplicationInfo()
                {
                    ApplicationId = _clientId,
                    ApplicationName = "AgileInspect",
                    ApplicationVersion = "1.0.0"
                };

                string mipDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgileInspect", "mip_data");
                if (!Directory.Exists(mipDataPath)) Directory.CreateDirectory(mipDataPath);

                var mipConfiguration = new MipConfiguration(appInfo, mipDataPath, LogLevel.Trace, false);
                _mipContext = Microsoft.InformationProtection.MIP.CreateMipContext(mipConfiguration);

                FileProfileSettings profileSettings = new FileProfileSettings(_mipContext, CacheStorageType.OnDisk, new ConsentDelegateImplementation());
                _fileProfile = await Microsoft.InformationProtection.MIP.LoadFileProfileAsync(profileSettings);

                var authDelegate = new BackgroundAuthDelegate(_clientId, _tenantId, _clientSecret);
                FileEngineSettings engineSettings = new FileEngineSettings("", authDelegate, "", "en-US") { Cloud = Cloud.Commercial };
                _fileEngine = await _fileProfile.AddEngineAsync(engineSettings);

                SyncAllLabels();

                _sdkInitialized = true;
                PluginContext.Log(pluginName, "MIP SDK initialized successfully.");
            }
            catch (Exception ex)
            {
                PluginContext.Log(pluginName, $"CRITICAL: Failed to initialize MIP SDK: {ex.Message}");
                throw;
            }
        }

        private void SyncAllLabels()
        {
            try
            {
                if (_fileEngine == null) return;
                bool discoveredNew = false;
                foreach (var label in _fileEngine.SensitivityLabels)
                {
                    string labelId = label.Id.ToLower();
                    if (!_labelNameCache.ContainsKey(labelId)) { _labelNameCache[labelId] = label.Name; discoveredNew = true; }
                    if (label.Children != null)
                    {
                        foreach (var child in label.Children)
                        {
                            string childId = child.Id.ToLower();
                            if (!_labelNameCache.ContainsKey(childId)) { _labelNameCache[childId] = $"{label.Name} / {child.Name}"; discoveredNew = true; }
                        }
                    }
                }
                if (discoveredNew) SaveLabelMap();
            }
            catch { }
        }

        public async Task<LabelResult> GetLabelFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            if (_sdkInitialized && _fileEngine != null)
            {
                try
                {
                    using (var handler = await _fileEngine.CreateFileHandlerAsync(filePath, filePath, true))
                    {
                        var label = handler.Label;
                        bool isProtected = handler.Protection != null;

                        if (label != null || isProtected)
                        {
                            string? labelId = label?.Label.Id.ToLower();
                            string? labelName = null;
                            if (label != null)
                            {
                                if (!_labelNameCache.TryGetValue(labelId!, out labelName))
                                {
                                    labelName = label.Label.Name;
                                    PluginContext.Log(pluginName, $"Warning: Detected Label ID {labelId} not found in synced labels.");
                                }
                            }

                            return new LabelResult
                            {
                                Id = labelId,
                                Name = labelName,
                                IsProtected = isProtected,
                                DetectionMethod = METHOD_SDK,
                                LastScannedAt = DateTime.Now
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginContext.Log(pluginName, $"SDK Scan Fallback for {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            var syncedIds = _labelNameCache.Keys.ToList();
            var detailedInfo = MetadataScanner.GetDetailedInfo(filePath, syncedIds);
            
            if (detailedInfo.HasData)
            {
                string labelId = detailedInfo.LabelId.ToLower();
                if (!_labelNameCache.TryGetValue(labelId, out string cachedName))
                {
                    PluginContext.Log(pluginName, $"Warning: Detected Offline Label ID {labelId} not found in synced labels.");
                }

                return new LabelResult
                {
                    Id = detailedInfo.LabelId,
                    Name = cachedName ?? detailedInfo.LabelName ?? $"Label ({detailedInfo.LabelId})",
                    Owner = detailedInfo.Owner,
                    TenantId = detailedInfo.TenantId,
                    IsProtected = detailedInfo.IsProtected,
                    DetectionMethod = detailedInfo.DetectionMethod,
                    LastScannedAt = DateTime.Now
                };
            }

            return new LabelResult { Name = null, Id = null, DetectionMethod = METHOD_NOT_FOUND };
        }
    }
}

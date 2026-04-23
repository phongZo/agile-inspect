using AgileInspect.Code.PluginContracts;
using AgileInspect.Code.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

namespace AgileInspect.Code
{
    public class PluginManager
    {
        #region Singleton
        public static PluginManager Instance { get; set; }
        public PluginManager()
        {
            Instance = this;
        }
        #endregion
        private readonly List<IAppPlugin> Plugins = new();
        private readonly List<IAppPlugin> _startedPlugins = new();
        private readonly List<AssemblyLoadContext> _loadContexts = new();
        private readonly object _sync = new();
        public AppCallbackHandler AppCallbackHandler { get; set; } = new AppCallbackHandler();
        public void LoadPlugins()
        {
            string pluginsDir = AppDomain.CurrentDomain.BaseDirectory;

            var rootPluginDir = Path.Combine(pluginsDir, "dll");

            PluginContext.SetCallback(AppCallbackHandler.Instance);

            if (!Directory.Exists(rootPluginDir))
            {
                DebugLog.WriteLine($"Plugin folder not found: {rootPluginDir}");
                return;
            }

            var settings = StoreCfgJson.Instance.eventSettings;
            var subDirs = Directory.GetDirectories(rootPluginDir);

            foreach (var subDir in subDirs)
            {
                var pluginDirName = Path.GetFileName(subDir);
                var eventType = StoreCfgLoader.mapPluginNameToEventType(pluginDirName);

                if (string.IsNullOrEmpty(eventType))
                {
                    DebugLog.WriteLine($"Skipped plugin folder (unmapped name): {pluginDirName}");
                    continue;
                }

                var setting = settings.FirstOrDefault(s => s.eventType == eventType);
                if (setting == null)
                {
                    DebugLog.WriteLine($"Skipped plugin (not in EventSettings): {eventType}");
                    continue;
                }

                var dllFiles = Directory.GetFiles(subDir, $"{pluginDirName}.dll");
                if (dllFiles.Length == 0)
                    continue;

                // Auto-detect if any DLL is Mixed-Mode (C++/CLI) to disable collectibility
                bool requiresNonCollectible = IsMixedModeRequired(subDir);
                var context = new PluginLoadContext(subDir, !requiresNonCollectible);
                _loadContexts.Add(context);

                foreach (var dll in dllFiles)
                {
                    try
                    {
                        var asm = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                        var types = asm.GetTypes()
                            .Where(t => typeof(IAppPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        foreach (var type in types)
                        {
                            if (Activator.CreateInstance(type) is IAppPlugin plugin)
                            {
                                // Merge operational data and secrets into a single JSON for the DLL
                                var mergedMap = new Dictionary<string, object>();
                                
                                // 1. Map standard parameters
                                if (setting.eventParams != null)
                                {
                                    foreach (var prop in typeof(EventParams).GetProperties())
                                    {
                                        mergedMap[prop.Name] = prop.GetValue(setting.eventParams);
                                    }
                                }

                                // 2. Map ALL fields defined in MIPConfig from License
                                foreach (var prop in typeof(MIPConfig).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                {
                                    string secretValue = License.GetSecret(prop.Name);
                                    if (!string.IsNullOrEmpty(secretValue))
                                    {
                                        mergedMap[prop.Name] = secretValue;
                                    }
                                }

                                string eventParamsJson = JsonSerializer.Serialize(mergedMap);
                                string triggerParamsJson = JsonSerializer.Serialize(setting.triggerParams);

                                plugin.Initialize();
                                plugin.SetParameters(eventParamsJson, setting.triggerType, triggerParamsJson);

                                Plugins.Add(plugin);
                                DebugLog.WriteLine($"Loaded plugin: {plugin.pluginName} as {eventType} from {dll} (Collectible: {!requiresNonCollectible})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteLine($"Failed to load plugin from {dll}: {ex.Message}");
                    }
                }
            }
        }

        public void StartAll()
        {
            foreach (var plugin in Plugins)
            {
                if (_startedPlugins.Contains(plugin))
                {
                    continue;
                }

                try
                {
                    plugin.Start();
                    _startedPlugins.Add(plugin);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to start plugin {plugin.pluginName}: {ex.Message}");
                }
            }
        }


        public void StopAll()
        {
            foreach (var plugin in Plugins)
            {
                if (!_startedPlugins.Contains(plugin))
                {
                    continue;
                }

                try
                {
                    plugin.Stop();
                    _startedPlugins.Remove(plugin);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to stop plugin {plugin.pluginName}: {ex.Message}");
                }
            }
        }

        public void ReloadAll()
        {
            lock (_sync)
            {
                StopAll();
                ClearLoadedPlugins();
                LoadPlugins();
                StartAll();
            }
        }

        private void ClearLoadedPlugins()
        {
            foreach (var ctx in _loadContexts)
            {
                try
                {
                    // Only unload if the context supports it
                    if (ctx.IsCollectible)
                        ctx.Unload();
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to unload plugin context: {ex.Message}");
                }
            }

            _loadContexts.Clear();
            _startedPlugins.Clear();
            Plugins.Clear();
        }

        public IEnumerable<T> GetPluginsOfType<T>() where T : IAppPlugin => Plugins.OfType<T>();

        private bool IsMixedModeRequired(string pluginDir)
        {
            try
            {
                var allDlls = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);
                foreach (var dllPath in allDlls)
                {
                    if (IsMixedModeAssembly(dllPath)) return true;
                }
            }
            catch { }
            return false;
        }

        private bool IsMixedModeAssembly(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var peReader = new PEReader(stream))
                {
                    if (!peReader.HasMetadata) return false;
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        return (corHeader.Flags & CorFlags.ILOnly) == 0;
                    }
                }
            }
            catch { }
            return false;
        }
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string pluginPath;
        private readonly string dependencyDir;

        public PluginLoadContext(string pluginPath, bool isCollectible)
            : base(isCollectible: isCollectible)
        {
            this.pluginPath = pluginPath;
            this.dependencyDir = Path.Combine(pluginPath, "libs");
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string depPath = Path.Combine(dependencyDir, $"{assemblyName.Name}.dll");
            if (File.Exists(depPath))
            {
                return LoadFromAssemblyPath(depPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string depPath = Path.Combine(dependencyDir, unmanagedDllName);
            if (!depPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) depPath += ".dll";
            if (File.Exists(depPath)) return LoadUnmanagedDllFromPath(depPath);

            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            string archPath = Path.Combine(dependencyDir, arch, unmanagedDllName);
            if (!archPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) archPath += ".dll";
            
            if (File.Exists(archPath)) return LoadUnmanagedDllFromPath(archPath);

            return IntPtr.Zero;
        }
    }
}

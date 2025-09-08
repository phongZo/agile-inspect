using AgileInspect.Code.PluginContracts;
using AgileInspect.Code.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace AgileInspect.Code
{
    public class PluginManager
    {
        #region Singleton
        private static readonly PluginManager _instance = new();
        public static PluginManager Instance => _instance;

        private PluginManager() { }
        #endregion
        private readonly List<IAppPlugin> Plugins = [];

        private readonly List<string> _startedPlugins = [];
        public void LoadPlugins()
        {

            string exePath = Environment.ProcessPath!;
            string baseDir = Path.GetDirectoryName(exePath)!;
            string rootPluginDir = Path.Combine(baseDir, "inspect", "dll");
            PluginContext.SetCallback(AppCallbackHandler.Instance);

            if (!Directory.Exists(rootPluginDir))
            {
                DebugLog.WriteLine($"Plugin folder not found: {rootPluginDir}");
                return;
            }

            var settings = StoreCfgJson.Instance.eventConfig.eventSettings;
            var subDirs = Directory.GetDirectories(rootPluginDir);

            foreach (var subDir in subDirs)
            {
                var pluginDirName = Path.GetFileName(subDir);
                var eventType = StoreCfgLoader.Instance.MapPluginNameToEventType(pluginDirName);

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

                var context = new PluginLoadContext(subDir);

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
                                string eventParamsJson = JsonSerializer.Serialize(setting.eventParams);
                                string triggerParamsJson = JsonSerializer.Serialize(setting.triggerParams);

                                plugin.Initialize();
                                plugin.SetParameters(eventParamsJson, setting.triggerType, triggerParamsJson);

                                Plugins.Add(plugin);
                                DebugLog.WriteLine($"Loaded plugin: {plugin.Name} as {eventType} from {dll}");
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
                if (_startedPlugins.Contains(plugin.Name))
                {
                    continue;
                }

                try
                {
                    plugin.Start();
                    _startedPlugins.Add(plugin.Name);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to start plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        public void StopAll()
        {
            foreach (var plugin in Plugins)
            {
                if (!_startedPlugins.Contains(plugin.Name))
                {
                    continue;
                }

                try
                {
                    plugin.Stop();
                    _startedPlugins.Remove(plugin.Name);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to stop plugin {plugin.Name}: {ex.Message}");
                }
            }

            Plugins.Clear();
        }

    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string pluginPath;
        private readonly string dependencyDir;

        public PluginLoadContext(string pluginPath)
            : base(isCollectible: false)
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
    }
}
using AgileInspect.Code.PluginContracts;
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
        private readonly List<IAppPlugin> Plugins = new();
        public AppCallbackHandler AppCallbackHandler {  get; set; } = new AppCallbackHandler();
        public void LoadPlugins(string folderPath)
        {
            PluginContext.SetCallback(AppCallbackHandler.Instance);

            if (!Directory.Exists(folderPath))
            {
                DebugLog.WriteLine($"Plugin folder not found: {folderPath}");
                return;
            }

            var settings = StoreCfgJson.Instance.EventConfig.EventSettings;

            var subDirs = Directory.GetDirectories(folderPath);
            foreach (var subDir in subDirs)
            {
                var folderName = Path.GetFileName(subDir);
                var dllFiles = Directory.GetFiles(subDir, $"{folderName}.dll");

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
                                string pluginName = plugin.Name;

                                var setting = settings.FirstOrDefault(s => s.EventType == pluginName);
                                if (setting == null)
                                {
                                    DebugLog.WriteLine($"Skipped plugin (not in EventSettings): {pluginName}");
                                    continue;
                                }

                                string eventParamsJson = JsonSerializer.Serialize(setting.EventParams);
                                string triggerParamsJson = JsonSerializer.Serialize(setting.TriggerParams);

                                plugin.Initialize();
                                plugin.SetCallback(AppCallbackHandler.Instance);
                                plugin.SetParameters(eventParamsJson, setting.TriggerType, triggerParamsJson);

                                Plugins.Add(plugin);
                                DebugLog.WriteLine($"Loaded plugin: {pluginName} from {dll}");
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
                try
                {
                    plugin.Start();
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
                try
                {
                    plugin.Stop();
                    DebugLog.WriteLine($"Stopped plugin: {plugin.GetType().FullName}");
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"Failed to stop plugin {plugin.GetType().FullName}: {ex.Message}");
                }
            }
        }

        public IEnumerable<T> GetPluginsOfType<T>() where T : IAppPlugin => Plugins.OfType<T>();
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string pluginDirectory;

        public PluginLoadContext(string pluginDirectory) : base(isCollectible: false)
        {
            this.pluginDirectory = pluginDirectory;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string dllPath = Path.Combine(pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(dllPath))
            {
                return LoadFromAssemblyPath(dllPath);
            }

            return null;
        }
    }
}
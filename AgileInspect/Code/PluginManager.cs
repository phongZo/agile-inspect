using AgileInspect.Code.PluginContracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace AgileInspect.Code
{
    public class PluginManager
    {
        private readonly List<IAppPlugin> Plugins = new();

        public void LoadPlugins(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                DebugLog.WriteLine($"Plugin folder not found: {folderPath}");
                return;
            }

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
                                plugin.Initialize();
                                Plugins.Add(plugin);
                                DebugLog.WriteLine($"Loaded plugin: {plugin.Name} from {dll}");
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
                    DebugLog.WriteLine($"Started plugin: {plugin.Name}");
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

        protected override Assembly? Load(AssemblyName assemblyName)
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
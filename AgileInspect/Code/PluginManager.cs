using AgileInspect.Code.PluginContracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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

            foreach (var dll in Directory.GetFiles(folderPath, "*Plugin.dll"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dll);
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
}

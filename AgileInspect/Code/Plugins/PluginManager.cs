using AgileInspect.Code.Plugins.PluginContracts;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AgileInspect.Code.Plugins
{
    internal class PluginManager
    {
        public static PluginManager Instance { get; } = new PluginManager();

        private PluginManager() { }

        private const string PluginFolderName = "inspect";

        public static void Main(string[] args)
        {
            DebugLog.WriteLine($"[PluginManager] Starting Plugin Manager");

            Instance.LoadAndRunPlugins();

            DebugLog.WriteLine($"[PluginManager] Finished executing plugins");
        }

        public void LoadAndRunPlugins()
        {
            string exePath = Environment.ProcessPath!;
            string baseDir = Path.GetDirectoryName(exePath)!;
            string pluginDir = Path.Combine(baseDir, PluginFolderName);

            if (!Directory.Exists(pluginDir))
            {
                DebugLog.WriteLine($"[PluginManager] Plugin folder not found: {pluginDir}");
                return;
            }

            foreach (string dllPath in Directory.GetFiles(pluginDir, "*.dll"))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllPath);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            DebugLog.WriteLine($"[PluginManager] Executing plugin: {plugin.Name}");
                            plugin.Execute();
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"[PluginManager] Failed to load {dllPath}: {ex.Message}");
                }
            }
        }
    }
}

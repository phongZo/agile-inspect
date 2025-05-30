using AgileInspect.Code;
using AgileInspect.Code.Settings;
using System;
using System.Windows;

namespace AgileInspect
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; set; }
        private readonly PluginManager PluginManager = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Hide();
            Instance = this;

            DebugLog.Write("", false);
            DebugLog.Write("--------AgileInspect Start-------");
            StoreCfgLoader.Load();
            string pluginsDir = AppDomain.CurrentDomain.BaseDirectory;
            PluginManager.LoadPlugins(pluginsDir);
            PluginManager.StartAll();
        }
    }
}

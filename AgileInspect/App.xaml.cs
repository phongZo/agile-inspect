using AgileInspect.Code;
using System.Windows;

namespace AgileInspect
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Permission Permission { get; set; } = new Permission();
        protected override void OnStartup(StartupEventArgs e)
        {
            DebugLog.Init();
            EventLog.Init();
            base.OnStartup(e);
        }
    }

}

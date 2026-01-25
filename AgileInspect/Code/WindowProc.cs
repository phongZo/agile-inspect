

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace AgileInspect.Code
{
    public class WindowProc
    {
        #region Singleton
        public static WindowProc Instance { get; set; }
        public WindowProc()
        {
            Instance = this;
        }
        #endregion

        public void Init(Window window)
        {

            var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
            if (hwndSource != null)
            {
                Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            }

        }

        private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            DebugLog.WriteLine("User switch " + e.Reason.ToString());
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect
                || e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteDisconnect
                || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock
                || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogoff)
            {
                DebugLog.WriteLine("Shutdow app");
                Application.Current.Shutdown();
            }

        }
      
    }
}

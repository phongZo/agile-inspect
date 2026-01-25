using AgileInspect.Code;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
            
            // Handle UI thread exceptions
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            // Handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            
            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("UI Thread Exception", e.Exception);
            e.Handled = true; // Prevent application crash, but log the exception
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("Unhandled Exception", ex);
            }
            else
            {
                DebugLog.WriteLine($"Unhandled Exception (non-Exception object): {e.ExceptionObject}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("Unobserved Task Exception", e.Exception);
            e.SetObserved(); // Mark as observed to prevent process crash
        }

        private void LogException(string exceptionType, Exception ex)
        {
            try
            {
                DebugLog.WriteLine("========================================");
                DebugLog.WriteLine($"CRITICAL: {exceptionType} occurred");
                DebugLog.WriteLine($"Exception Type: {ex.GetType().FullName}");
                DebugLog.WriteLine($"Message: {ex.Message}");
                DebugLog.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    DebugLog.WriteLine($"Inner Exception: {ex.InnerException.GetType().FullName}");
                    DebugLog.WriteLine($"Inner Message: {ex.InnerException.Message}");
                    DebugLog.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                DebugLog.WriteLine("========================================");
            }
            catch
            {
                // If logging fails, try to write to console as last resort
                Console.WriteLine($"Failed to log exception: {ex}");
            }
        }
    }

}

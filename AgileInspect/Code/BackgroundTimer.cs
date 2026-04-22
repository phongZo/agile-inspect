using System;
using System.Threading;

namespace AgileInspect.Code
{
    public class BackgroundTimer
    {
        private Timer Timer;
        readonly System.Action Callback;
        private string _functionName = string.Empty;
        private bool _isRunning;

        public TimeSpan Interval { get; set; }

        public BackgroundTimer(System.Action callback, string functionName)
        {
            Callback = callback;
            _functionName = functionName;
        }

        public void Start(int intervalInSeconds)
        {
            Interval = TimeSpan.FromSeconds(intervalInSeconds);
            Timer = new Timer(TimerCallback, null, TimeSpan.Zero, Interval);
            _isRunning = true;
        }

        public void StopIfRunning()
        {
            if (_isRunning)
            {
                DebugLog.WriteLine($"STOP {_functionName} timer");
                Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                _isRunning = false;
            }
        }

        public void Stop()
        {
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
        }

        private void TimerCallback(object state)
        {
            try
            {
                Callback(); 
            }
            catch (Exception exc)
            {
                DebugLog.WriteLine("Timer Exception: " + exc.Message);
                StopIfRunning();
            }
            finally
            {
                if (_isRunning)
                {
                    Timer?.Change(Interval, Interval);
                }
            }
        }
    }
}
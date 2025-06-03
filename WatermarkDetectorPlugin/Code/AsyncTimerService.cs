namespace WatermarkDetectorPlugin {
    public class AsyncTimerService : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly Func<Task> _action;
        private bool _isProcessing = false;
        private readonly bool _runImmediately;

        public AsyncTimerService(double intervalMs, Func<Task> action, bool runImmediately = true)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _runImmediately = runImmediately;

            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += async (sender, e) => await TimerElapsedAsync();
            _timer.AutoReset = true;
        }

        private async Task TimerElapsedAsync()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            try
            {
                await _action();
            }
            catch (Exception ex)
            {
                PluginContext.Log(GetType().Namespace, $"[AsyncTimerService] Error: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public void Start()
        {
            if (_runImmediately)
            {
                _ = TimerElapsedAsync();
            }
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }

}

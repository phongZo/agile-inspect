using AgileInspect;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class AsyncTimerService : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly Func<Task> _action;
    private readonly ConcurrentQueue<int> _queue = new();
    private bool _isProcessing = false;
    private readonly bool _runImmediately;

    public AsyncTimerService(double intervalMs, Func<Task> action, bool runImmediately = true)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += (sender, e) => TimerElapsed();
        _timer.AutoReset = true;
        _runImmediately = runImmediately;
    }

    private void TimerElapsed()
    {
        _queue.Enqueue(1);
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            while (_queue.TryDequeue(out _))
            {
                try
                {
                    await _action();
                }
                catch (Exception ex)
                {
                    DebugLog.WriteLine($"[AsyncTimerQueueService] Error: {ex}");
                }
            }
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
            // Gọi ngay callback mà không chờ timer
            _queue.Enqueue(1);
            _ = ProcessQueueAsync();
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

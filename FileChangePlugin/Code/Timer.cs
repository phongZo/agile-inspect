public class TimerWrapper
{
    private readonly System.Timers.Timer _timer;
    private readonly Action Callback;

    public bool IsEnable => _timer.Enabled;

    public TimeSpan Interval
    {
        get => TimeSpan.FromMilliseconds(_timer.Interval);
        set => _timer.Interval = value.TotalMilliseconds;
    }

    public TimerWrapper(Action callback)
    {
        Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _timer = new System.Timers.Timer();
        _timer.Elapsed += TimerElapsed;
        _timer.AutoReset = true;
    }

    public void Start(int intervalInSeconds)
    {
        _timer.Interval = intervalInSeconds * 1000;
        _timer.Start();
    }

    private void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            Callback();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Timer Exception: " + ex.Message);
            StopIfRunning();
        }
    }

    public void StopIfRunning()
    {
        if (_timer.Enabled)
        {
            Console.WriteLine("STOP Timer");
            _timer.Stop();
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }
}

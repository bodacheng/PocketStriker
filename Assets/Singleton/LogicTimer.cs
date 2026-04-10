using System;
using System.Threading;

public class SafeTimer
{
    private static SynchronizationContext _unityContext;
    private Timer _timer;
    private readonly object _lock = new();

    public SafeTimer()
    {
        _unityContext ??= SynchronizationContext.Current;
    }

    public void StartTimer(float seconds, Action onTimeout)
    {
        Stop();

        int ms = (int)(seconds * 1000);
        _timer = new Timer(_ =>
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }

            _unityContext?.Post(_ =>
            {
                try
                {
                    onTimeout?.Invoke();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }, null);
        }, null, ms, Timeout.Infinite);
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}

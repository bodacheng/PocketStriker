using System;
using System.Threading;

public class SafeTimer : IDisposable
{
    private static SynchronizationContext _unityContext;
    private Timer _timer;
    private readonly object _lock = new();

    public SafeTimer()
    {
        // 在构造函数里记录主线程上下文（仅当首次构造时）
        _unityContext ??= SynchronizationContext.Current;
    }

    public void StartTimer(float seconds, Action onTimeout)
    {
        Stop(); // 清理旧定时器

        var ms = Math.Max(0, (int)(seconds * 1000));
        _timer = new Timer(_ =>
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }

            if (_unityContext != null)
            {
                // 确保回调在主线程执行
                _unityContext.Post(_ => InvokeSafely(onTimeout), null);
                return;
            }

            InvokeSafely(onTimeout);
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

    public void Dispose()
    {
        Stop();
    }

    static void InvokeSafely(Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }
    }
}

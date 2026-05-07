using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

public class SingleThreadProcessor
{
    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
    private readonly object counterLock = new object();
    private int pendingCount;

    public int TaskRunningCount
    {
        get
        {
            lock (counterLock)
            {
                return pendingCount;
            }
        }
    }

    public async UniTask RunAsQueued(Func<UniTask> originFactory)
    {
        if (originFactory == null)
            throw new ArgumentNullException(nameof(originFactory));

        lock (counterLock)
        {
            pendingCount++;
        }

        await semaphore.WaitAsync();
        try
        {
            await originFactory();
        }
        finally
        {
            semaphore.Release();
            lock (counterLock)
            {
                pendingCount--;
            }
        }
    }

    public async void RunAsQueued(Func<UniTask> originFactory, UnityAction afterToDo)
    {
        await RunAsQueued(originFactory);
        afterToDo?.Invoke();
    }
}

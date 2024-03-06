using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

public class SingleThreadProcessor
{
    public int TaskRunningCount => processList.Count;
    
    public async UniTask RunAsQueued(UniTask origin)
    {
        await WaitForTurn(origin);
    }

    public async void RunAsQueued(UniTask origin, UnityAction afterToDo)
    {
        await WaitForTurn(origin);
        afterToDo();
    }
    
    private readonly List<UniTask> processList = new List<UniTask>();
    private readonly object processListLock = new object();
    private async UniTask WaitForTurn(UniTask origin)
    {
        await UniTask.WaitUntil(() =>
        {
            lock (processListLock)
            {
                return processList.Count == 0;
            }
        });

        lock (processListLock)
        {
            processList.Add(origin);
        }

        await UniTask.WaitUntil(() =>
        {
            lock (processListLock)
            {
                return processList.Contains(origin) && processList.IndexOf(origin) == 0;
            }
        });

        try
        {
            await origin;
        }
        finally
        {
            lock (processListLock)
            {
                processList.RemoveAt(0);
            }
        }
    }
}

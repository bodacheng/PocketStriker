using System.Collections.Generic;
using UniRx;

public class SingleAssignmentDisposableCleaner
{
    static readonly List<SingleAssignmentDisposable> list = new List<SingleAssignmentDisposable>();
    public static void Add(SingleAssignmentDisposable single)
    {
        if (!list.Contains(single))
        {
            list.Add(single);
        }
    }

    public static void Clear()
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] != null && !list[i].IsDisposed)
            {
                list[i].Dispose();
            }
        }
        list.Clear();
    }
}

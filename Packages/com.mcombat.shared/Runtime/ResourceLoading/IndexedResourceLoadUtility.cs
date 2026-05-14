using System;
using Cysharp.Threading.Tasks;

public static class IndexedResourceLoadUtility
{
    public static UniTask<T> LoadIfKeyExists<T>(
        string label,
        string key,
        Func<string, string, bool> keyExists,
        Func<string, UniTask<T>> load)
    {
        if (string.IsNullOrEmpty(label) ||
            string.IsNullOrEmpty(key) ||
            keyExists == null ||
            load == null ||
            !keyExists(label, key))
        {
            return default;
        }

        return load(key);
    }
}

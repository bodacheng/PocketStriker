using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class UnitIconLoader
{
    public static async UniTask<Sprite> Load(
        string recordId,
        GameObject memoryReleaseTarget = null,
        Func<string, bool> exists = null,
        Func<string, GameObject, UniTask<Sprite>> load = null)
    {
        if (exists != null && !exists(recordId))
        {
            return null;
        }

        var key = "unit/" + recordId;
        return load != null
            ? await load(key, memoryReleaseTarget)
            : await AddressablesAssetLoader.LoadT<Sprite>(key, memoryReleaseTarget);
    }
}

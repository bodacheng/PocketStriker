using System;
using Cysharp.Threading.Tasks;
using Singleton;
using UnityEngine;

public class UnitCreator {
    
    public static async UniTask<Data_Center> CreateUnit(UnitInfo info, int preloadCount, Action<float> onProgress = null)
    {
        onProgress?.Invoke(0f);
        var dataCenter = await GeneralModelPool.GetModel(
            info.r_id,
            onProgress: progress => onProgress?.Invoke(Mathf.Lerp(0f, 0.45f, progress)));
        if (dataCenter == null)
        {
            Debug.Log("严重资源类错误");
            return dataCenter;
        }
        onProgress?.Invoke(0.45f);
        var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(info.r_id));
        await dataCenter.Step2Initialize(
            unitConfig.TYPE,
            unitConfig.element,
            info.set,
            preloadCount,
            progress => onProgress?.Invoke(Mathf.Lerp(0.45f, 1f, progress)));
        onProgress?.Invoke(1f);
        return dataCenter;
    }
}

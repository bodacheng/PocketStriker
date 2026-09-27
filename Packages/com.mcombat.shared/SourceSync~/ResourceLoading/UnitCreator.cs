using System;
using Cysharp.Threading.Tasks;
using Singleton;
using UnityEngine;

public class UnitCreator {
    
    public static async UniTask<Data_Center> CreateUnit(UnitInfo info, int preloadCount, Action<float> onProgress = null)
    {
        var dataCenter = await GeneralModelPool.GetModel(info.r_id);
        if (dataCenter == null)
        {
            Debug.Log("严重资源类错误");
            return dataCenter;
        }
        onProgress?.Invoke(0.5f);
        var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(info.r_id));
        await dataCenter.Step2Initialize(unitConfig.TYPE, unitConfig.element, info.set, preloadCount);
        onProgress?.Invoke(1f);
        return dataCenter;
    }
}

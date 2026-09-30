using System;
using Cysharp.Threading.Tasks;
using Singleton;
using UnityEngine;

public class UnitCreator {
    
    public static async UniTask<Data_Center> CreateUnit(UnitInfo info, int preloadCount, Action<float> onProgress = null,
        Vector3? stagingPosition = null)
    {
        onProgress?.Invoke(0f);
        var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(info.r_id));
        if (unitConfig == null)
        {
            throw new InvalidOperationException($"Missing unit configuration: unit {info.r_id}; model key unavailable.");
        }
        var modelKey = unitConfig.TYPE + "/" + unitConfig.REAL_NAME;
        var dataCenter = await GeneralModelPool.GetModel(
            info.r_id,
            pos: stagingPosition ?? Vector3.zero,
            onProgress: progress => onProgress?.Invoke(Mathf.Lerp(0f, 0.45f, progress)));
        if (dataCenter == null)
        {
            throw new InvalidOperationException($"Unit model preparation returned no Data_Center: unit {info.r_id}, model '{modelKey}'.");
        }
        onProgress?.Invoke(0.45f);
        try
        {
            await dataCenter.Step2Initialize(
                unitConfig.TYPE,
                unitConfig.element,
                info.set,
                preloadCount,
                progress => onProgress?.Invoke(Mathf.Lerp(0.45f, 1f, progress)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = dataCenter == null
                ? "Unit model was destroyed during battle initialization"
                : "Could not initialize unit for battle";
            throw new InvalidOperationException($"{reason}: unit {info.r_id}, model '{modelKey}'.", exception);
        }
        if (dataCenter == null)
        {
            throw new InvalidOperationException($"Unit model was destroyed during battle initialization: unit {info.r_id}, model '{modelKey}'.");
        }
        onProgress?.Invoke(1f);
        return dataCenter;
    }
}

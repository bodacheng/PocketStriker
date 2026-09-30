using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Singleton
{
    public static class GeneralModelPool
    {
        public static async UniTask<Data_Center> GetModel(string rId, Transform parent = null, Vector3 pos = new Vector3(), Action<float> onProgress = null)
        {
            onProgress?.Invoke(0f);
            //以上这个信息就包括了全部的“我的角色”信息，下面别的信息都是据此各种由此索引出来的。
            var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(rId));
            if (unitConfig == null)
            {
                throw new InvalidOperationException($"Missing unit configuration: unit {rId}; model key unavailable.");
            }

            var modelKey = unitConfig.TYPE + "/" + unitConfig.REAL_NAME;
            var tempModel = await AddressablesLogic.LoadObject(
                modelKey,
                pos,
                progress => onProgress?.Invoke(Mathf.Lerp(0.05f, 0.45f, progress)));
            onProgress?.Invoke(0.45f);
            if (tempModel == null)
                throw new InvalidOperationException($"Could not load unit model: unit {rId}, model '{modelKey}'.");
            tempModel.transform.SetParent(parent);
            var odl = tempModel.GetComponent<OutsideDataLink>();
            if (odl == null)
            {
                throw new InvalidOperationException($"Unit model is missing root OutsideDataLink: unit {rId}, model '{modelKey}'.");
            }
            var d = odl._C;        
            if (d == null)
            {
                throw new InvalidOperationException($"Unit model OutsideDataLink._C is missing: unit {rId}, model '{modelKey}'.");
            }
            
            // 在角色生成的瞬间各个组件的awake和onenable就已经都开了，而一些数据的初始化是从下一行开始，所以要确保这个过程不会有一些因为变量没被初始化而形成的报错。
            d.element = unitConfig.element;
            try
            {
                await d.Step1Initialize(
                    unitConfig.TYPE,
                    unitConfig.BASIC_MOVEMENT_PACK,
                    progress => onProgress?.Invoke(Mathf.Lerp(0.45f, 0.95f, progress)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var reason = tempModel == null || d == null
                    ? "Unit model was destroyed during initialization"
                    : "Could not initialize unit model";
                throw new InvalidOperationException($"{reason}: unit {rId}, model '{modelKey}'.", exception);
            }
            if (tempModel == null || d == null)
            {
                throw new InvalidOperationException($"Unit model was destroyed during initialization: unit {rId}, model '{modelKey}'.");
            }
            tempModel.SetActive(true);
            onProgress?.Invoke(1f);
            return d;
        }
    }
}

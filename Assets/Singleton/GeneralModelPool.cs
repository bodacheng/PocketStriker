using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Singleton
{
    public static class GeneralModelPool
    {
        public static async UniTask<Data_Center> GetModel(string rId, Transform parent = null, Vector3 pos = new Vector3())
        {
            //以上这个信息就包括了全部的“我的角色”信息，下面别的信息都是据此各种由此索引出来的。
            var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(rId));
            if (unitConfig == null)
            {
                Debug.Log("资源号码错误");
                return null;
            }

            var tempModel = await AddressablesLogic.LoadObject(unitConfig.TYPE + "/" + unitConfig.REAL_NAME, pos);
            tempModel.transform.SetParent(parent);
            var odl = tempModel.GetComponent<OutsideDataLink>();
            if (odl == null)
            {
                return null;
            }
            var d = odl._C;        
            
            // 在角色生成的瞬间各个组件的awake和onenable就已经都开了，而一些数据的初始化是从下一行开始，所以要确保这个过程不会有一些因为变量没被初始化而形成的报错。
            d.element = unitConfig.element;
            await (d.Step1Initialize(unitConfig.TYPE, unitConfig.BASIC_MOVEMENT_PACK));
            if (tempModel == null || d == null)
            {
                return null;
            }
            tempModel.SetActive(true);
            return d;
        }
    }
}
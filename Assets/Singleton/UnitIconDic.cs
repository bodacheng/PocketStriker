using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Singleton
{
    public static class UnitIconDic {
        
        public static async UniTask<Sprite> Load(string recordId, GameObject memoryReleaseTarget = null)
        {
            var unitConfig = Units.GetUnitConfig(recordId);
            if (unitConfig == null)
                return null;
            var sprite = await AddressablesLogic.LoadT<Sprite>("unit/" + recordId, memoryReleaseTarget);
            return sprite;
        }
    }
}

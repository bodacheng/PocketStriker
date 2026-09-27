using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Singleton
{
    public static class UnitIconDic {
        
        public static async UniTask<Sprite> Load(string recordId, GameObject memoryReleaseTarget = null)
        {
            return await UnitIconLoader.Load(
                recordId,
                memoryReleaseTarget,
                id => Units.GetUnitConfig(id) != null,
                (key, target) => AddressablesLogic.LoadT<Sprite>(key, target));
        }
    }
}

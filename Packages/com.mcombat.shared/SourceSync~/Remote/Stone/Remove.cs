using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;

namespace dataAccess
{
    public static partial class Stones
    {
        // 删除一个技能石

        public static async UniTask RemoveStoneLocal(string instanceId)
        {
            if (RenderModelDic.TryGetValue(instanceId, out var renderModel) && renderModel != null)
            {
                var worldPos = PosCal.GetWorldPos(PreScene.target.postProcessCamera, renderModel.GetComponent<RectTransform>(), 0f);
                var path = FightGlobalSetting.EffectPathDefine();
                var slotEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion3.prefab");
                slotEffect.gameObject.name = "stoneExplosion";
                slotEffect.gameObject.transform.position = worldPos;
                slotEffect.Play(true);

                Object.Destroy(renderModel.gameObject);
            }
            RenderModelDic.Remove(instanceId);
            Dic.Remove(instanceId);
        }
    }
}

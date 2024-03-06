using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static class SkillIcon
{
    public static async UniTask<GameObject> FindSkillIcon(string skillId)
    {
        var prefab = GetDefaultSkillIconByResource();
        var returnValue = Object.Instantiate(prefab);
        var sprite = await AddressablesLogic.LoadT<Sprite>(skillId, returnValue);
        if (returnValue == null)
            return null;
        if (sprite != null)
            returnValue.GetComponent<Image>().sprite = sprite;
        return returnValue;
    }
    
    static GameObject GetDefaultSkillIconByResource()
    {
        var _d = Resources.Load<GameObject>("BasicSprites/stoneModel");
        return _d;
    }
}

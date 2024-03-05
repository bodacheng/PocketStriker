using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using NoSuchStudio.Common;
using UnityEngine;

public class StoneBtnEffect : MonoBehaviour
{
    [SerializeField] private RectTransform effectT1;
    [SerializeField] private RectTransform effectT2;
    
    private readonly List<string> effect1List = new List<string>
    {
        "ButtonEffects/darkmagic/normal.prefab",
        "ButtonEffects/bluemagic/normal.prefab",
        "ButtonEffects/redmagic/normal.prefab",
        "ButtonEffects/greenmagic/normal.prefab",
        "ButtonEffects/lightmagic/normal.prefab",
    };
    
    private readonly List<string> effect2List = new List<string>
    {
        "ButtonEffects/darkmagic/EX3.prefab",
        "ButtonEffects/bluemagic/EX3.prefab",
        "ButtonEffects/redmagic/EX3.prefab",
        "ButtonEffects/greenmagic/EX3.prefab",
        "ButtonEffects/lightmagic/EX3.prefab",
    };
    
    // Start is called before the first frame update
    void Start()
    {
        SlotShine(effect1List.Random(), effectT1);
        SlotShine(effect2List.Random(), effectT2);
    }

    async void SlotShine(string effectName, RectTransform t)
    {
        var effect = await AddressablesLogic.LoadTOnObject<ParticleSystem>(effectName);
        if (t == null)
        {
            Destroy(effect.gameObject);
            return;
        }
        effect.transform.SetParent(t);
        effect.transform.position = 
            PosCal.GetWorldPos(PreScene.target.postProcessCamera, t.GetComponent<RectTransform>(), 20f);
        effect.gameObject.SetActive(true);
    }
}

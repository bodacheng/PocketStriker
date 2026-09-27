using mainMenu;
using UnityEngine;
public class ArenaBtnEffect : MonoBehaviour
{
    [SerializeField] private RectTransform effectT;
    [SerializeField] string effectName;
    // Start is called before the first frame update

    // anim event
    public void FistClash()
    {
        SlotShine(effectT);
    }

    private ParticleSystem effect;
    async void SlotShine(RectTransform t)
    {
        if (effect != null)
            Destroy(effect.gameObject);
        effect = await AddressablesLogic.LoadTOnObject<ParticleSystem>(effectName);
        if (t == null)
        {
            Destroy(effect.gameObject);
            return;
        }
        effect.transform.SetParent(t);
        effect.transform.position = 
            PosCal.GetWorldPos(PreScene.target.postProcessCamera, t.GetComponent<RectTransform>(), 20f);
    }
}

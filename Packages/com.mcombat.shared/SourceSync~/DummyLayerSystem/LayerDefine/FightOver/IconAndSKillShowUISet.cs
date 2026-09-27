using UnityEngine;

public class IconAndSKillShowUISet : MonoBehaviour
{
    [SerializeField] RectTransform iconPlace;
    [SerializeField] RectTransform nineSKillPlace;
    
    public void Set(SideUnitIcon sideUnitIcon, NineForShow nineForShow)
    {
        sideUnitIcon.transform.SetParent(iconPlace);
        sideUnitIcon.transform.localPosition = Vector3.zero;
        sideUnitIcon.transform.localScale = Vector3.one;
        sideUnitIcon.gameObject.SetActive(true);
        
        nineForShow.transform.SetParent(nineSKillPlace);
        nineForShow.transform.localPosition = Vector3.zero;
        nineForShow.transform.localScale = Vector3.one;
        nineForShow.gameObject.SetActive(true);
    }
}

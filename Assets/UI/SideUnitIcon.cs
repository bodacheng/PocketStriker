using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SideUnitIcon : MonoBehaviour {
    
    [SerializeField] Slider hpBar;
    [SerializeField] Text hpText;
    [SerializeField] Slider resistBar;
    [SerializeField] GameObject[] charges;
    [SerializeField] GameObject dreamComboFlg;
    [SerializeField] HeroIcon focusingCharIcon;
    [SerializeField] Text teamIndicator;
    
    [SerializeField] RectTransform hpBarHanger;

    public HeroIcon Icon => focusingCharIcon;
    public Text TeamIndicator => teamIndicator;
    public GameObject DreamComboFlg => dreamComboFlg;
    private Tweener resistBarTweener;
    public void RefreshResistanceBar(float resistance)
    {
        resistBarTweener?.Kill();
        resistBarTweener = DOTween.To(() => resistBar.value, (x) => resistBar.value = x, resistance / 10f, 0.2f);
    }
    
    private Tweener hpBarTweener;
    public void RefreshHpBar(float currentHp, float wholeHp)
    {
        hpText.text = Mathf.Ceil(currentHp).ToString();
        hpBarTweener?.Kill();
        hpBarTweener = DOTween.To(() => hpBar.value, (x) => hpBar.value = x, currentHp / wholeHp, 0.2f);
    }

    void OnDestroy()
    {
        resistBarTweener?.Kill();
        hpBarTweener?.Kill();
    }

    public void RefreshExBar(int currentEx)
    {
        if (currentEx >= 90)
        {
            charges[2].SetActive(true);
        }else{
            charges[2].SetActive(false);
        }
        
        if (currentEx >= 60)
        {
            charges[1].SetActive(true);
        }else{
            charges[1].SetActive(false);
        }
        
        if (currentEx >= 30)
        {
            charges[0].SetActive(true);
        }else{
            charges[0].SetActive(false);
        }
    }
    
    public void RecallBars()
    {
        hpBar.transform.SetParent(hpBarHanger);
        hpBar.transform.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        hpBar.transform.localScale = Vector3.one;
        resistBar.transform.SetParent(hpBarHanger);
        resistBar.transform.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        resistBar.transform.localScale = Vector3.one;
    }

    public void GreyOut()
    {
        hpBar.gameObject.SetActive(false);
        hpText.gameObject.SetActive(false);
        resistBar.gameObject.SetActive(false);
        teamIndicator.gameObject.SetActive(false);
        focusingCharIcon.CooldownCurtainUpdate(1);
    }
}

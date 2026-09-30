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
    public RectTransform HealthBarRect => hpBar != null ? hpBar.transform as RectTransform : null;
    // The charge images can be inactive at zero energy; their common parent
    // still describes the HUD area that the tutorial needs to point at.
    public RectTransform EnergyBarRect => charges != null && charges.Length > 0 && charges[0] != null
        ? charges[0].transform.parent as RectTransform
        : null;
    private Tweener resistBarTweener;
    bool _battleHUDStyled;
    bool _floatingHUD;
    bool _groupHUD;

    /// <summary>Uses the same compact status geometry for live battles and HUD previews.</summary>
    public void ApplyBattleHUDStyle(bool floating, bool groupBattle)
    {
        _battleHUDStyled = true;
        _floatingHUD = floating;
        _groupHUD = groupBattle;
        var root = (RectTransform)transform;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.sizeDelta = floating ? new Vector2(groupBattle ? 60 : 94, 32) : new Vector2(128, 170);
        focusingCharIcon.gameObject.SetActive(!floating);
        if (!floating)
        {
            Place((RectTransform)focusingCharIcon.transform, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(108, 108), new Vector2(0.5f, 1));
            focusingCharIcon.ApplyPreparationStyle();
            focusingCharIcon.RefreshPresentationSize();
        }
        var width = floating ? (groupBattle ? 60f : 94f) : 108f;
        Place(hpBarHanger, floating ? new Vector2(0.5f, 0) : new Vector2(0.5f, 1),
            floating ? Vector2.zero : new Vector2(0, -116), new Vector2(width, floating ? 9 : 12), new Vector2(0.5f, 1));
        StyleBar(hpBar, width, floating ? (groupBattle ? 6 : 9) : 12,
            new Color(0.40f, 0.78f, 0.65f), Vector2.zero);
        StyleBar(resistBar, width, floating ? 3 : 5,
            new Color(0.70f, 0.74f, 0.79f), new Vector2(0, floating ? -10 : -13));
        hpText.gameObject.SetActive(false);
        var energy = EnergyBarRect;
        if (energy != null)
        {
            Place(energy, floating ? new Vector2(0.5f, 0) : new Vector2(0.5f, 1),
                floating ? new Vector2(0, -21) : new Vector2(0, -145), new Vector2(width, floating ? 6 : 8), new Vector2(0.5f, 1));
            energy.gameObject.SetActive(!floating || !groupBattle);
            var chipWidth = (width - 8) / 3;
            for (var i = 0; i < charges.Length; i++)
            {
                Place((RectTransform)charges[i].transform, new Vector2(0, 1), new Vector2(i * (chipWidth + 4), 0),
                    new Vector2(chipWidth, floating ? 6 : 8), new Vector2(0, 1));
                foreach (var image in charges[i].GetComponentsInChildren<Image>(true))
                {
                    PreparationButtonSkin.Fit(image.rectTransform);
                    SetFlatImage(image, new Color(0.39f, 0.70f, 0.81f));
                }
            }
        }
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            if (!graphic.transform.IsChildOf(focusingCharIcon.transform)) graphic.raycastTarget = false;
        teamIndicator.fontSize = 20;
        teamIndicator.text = "▼";
        teamIndicator.color = new Color(0.91f, 0.81f, 0.49f);
        teamIndicator.alignment = TextAnchor.MiddleCenter;
        teamIndicator.rectTransform.sizeDelta = new Vector2(30, 28);
        foreach (var shadow in teamIndicator.GetComponents<Shadow>()) shadow.enabled = false;
        if (dreamComboFlg != null && dreamComboFlg.transform is RectTransform flag)
            Place(flag, new Vector2(1, 1), new Vector2(-6, -6), new Vector2(20, 20), new Vector2(1, 1));
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    public void SetBattleHUDTeamColor(bool player)
    {
        var fill = hpBar.fillRect != null ? hpBar.fillRect.GetComponent<Image>() : null;
        if (fill != null) fill.color = player ? new Color(0.40f, 0.78f, 0.65f) : new Color(0.83f, 0.48f, 0.41f);
    }

    static void SetFlatImage(Image image, Color color)
    {
        image.sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4;
        image.color = color;
        image.raycastTarget = false;
        foreach (var shadow in image.GetComponents<Shadow>()) shadow.enabled = false;
    }

    static void StyleBar(Slider slider, float width, float height, Color color, Vector2 offset)
    {
        Place((RectTransform)slider.transform, new Vector2(0.5f, 0.5f), offset, new Vector2(width, height), new Vector2(0.5f, 0.5f));
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        var background = slider.GetComponent<Image>();
        if (background != null) SetFlatImage(background, new Color(0.06f, 0.09f, 0.12f, 0.78f));
        if (slider.fillRect != null)
        {
            var area = slider.fillRect.parent as RectTransform;
            if (area != null && area != slider.transform) PreparationButtonSkin.Fit(area, 1, 1);
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) SetFlatImage(fill, color);
        }
        if (slider.handleRect != null) slider.handleRect.gameObject.SetActive(false);
    }
    public void RefreshResistanceBar(float resistance)
    {
        resistBarTweener?.Kill();
        resistBarTweener = DOTween.To(() => resistBar.value, (x) => resistBar.value = x, resistance / 10f, 0.2f);
    }
    
    private Tweener hpBarTweener;
    public void RefreshHpBar(float currentHp, float wholeHp)
    {
        //hpText.text = Mathf.Ceil(currentHp).ToString();
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
        if (_battleHUDStyled) ApplyBattleHUDStyle(_floatingHUD, _groupHUD);
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

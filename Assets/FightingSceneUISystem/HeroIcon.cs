using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Singleton;
using UnityEngine.EventSystems;

public class HeroIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {

    public BOButton iconButton;
    [SerializeField] Image icon;
    [SerializeField] Image iconBg;
    [SerializeField] Image frame;
    [SerializeField] Image cooldownCurtain;
    [SerializeField] GameObject warnFlag;
    
    public UnitConfig unitConfig;
    public GameObject WarnFlag => warnFlag;

    public string InstanceID
    {
        get;
        set;
    }
    
    protected void Grey()
    {
        frame.color = new Color(frame.color.r, frame.color.g, frame.color.b, 0.3f);
        iconBg.color = new Color(iconBg.color.r, iconBg.color.g, iconBg.color.b, 0.3f);
        icon.color = new Color(1,1,1,0.3f);
    }
    
    protected void LightOn()
    {
        frame.color = new Color(frame.color.r, frame.color.g, frame.color.b, 1f);
        iconBg.color = new Color(iconBg.color.r, iconBg.color.g, iconBg.color.b, 1f);
        icon.color = new Color(1,1,1,1f);
    }
    
    public void CooldownCurtainUpdate(float proportion)
    {
        cooldownCurtain.fillAmount = proportion;
    }

    public async void ChangeIcon(UnitInfo unitInfo, bool withSkillCheck = false, Func<int> teamCountGet = null)
    {
        var unitInfoFormal = UnitInfo.GetUnitInfo(unitInfo);
        if (unitInfo != null)
        {
            this.unitConfig = Units.GetUnitConfig(unitInfo.r_id);
            var pic = await UnitIconDic.Load(unitInfo.r_id, gameObject);
            if (this == null)
                return;
            ChangeIcon(pic, unitConfig.element);
            LightOn();
            var skillEditCheck = unitInfoFormal.set.CheckEdit();
            if ((withSkillCheck && skillEditCheck != SkillSet.SkillEditError.Perfect) 
                ||
                (teamCountGet != null && teamCountGet() == 0))
            {
                Grey();
            }
            
            var unitListItem = transform.GetComponent<UnitListItem>();
            if (unitListItem != null)
            {
                unitListItem.DecideSkillEquipFlg(skillEditCheck);
            }
        }
        else
        {
            ChangeIcon(null, Element.Null);
        }
    }
    
    public async void ChangeIcon(string recordId)
    {
        this.unitConfig = Units.GetUnitConfig(recordId);
        var pic = await UnitIconDic.Load(recordId, gameObject);
        ChangeIcon(pic, unitConfig.element);
    }
    
    public void Clear()
    {
        ChangeIcon(null, Element.Null);
    }
    
    void AdjustSize(Image icon)
    {
        var sprite = icon.sprite;
        if (sprite == null)
            return;
        var iconRect = icon.GetComponent<RectTransform>();
        var wholeParentRect = transform.GetComponent<RectTransform>();
        float spriteAspectRatio = sprite.rect.width / sprite.rect.height;
        
        if (spriteAspectRatio < 1)
        {
            iconRect.sizeDelta = new Vector2(
                sprite.rect.width * wholeParentRect.rect.height / sprite.rect.height, 
                wholeParentRect.rect.height);
        }
        else
        {
            iconRect.sizeDelta = new Vector2(
                wholeParentRect.rect.width, 
                sprite.rect.height * wholeParentRect.rect.width / sprite.rect.width);
        }
    }
    
    async void ChangeIcon(Sprite sprite, Element element)
    {
        //Icon.GetComponent<RectTransform>().sizeDelta = new Vector2(frame.GetComponent<RectTransform>().sizeDelta.x * 0.8f, frame.GetComponent<RectTransform>().sizeDelta.y * 0.8f);
        // if (cooldownCurtain != null)
        // {
        //     cooldownCurtain.transform.SetSiblingIndex(icon.transform.GetSiblingIndex() - 1);
        // }

        Color color = Color.clear, frameColor = Color.clear;
        switch (element)
        {
            case Element.blueMagic:
                color = CommonSetting._blueBgColor;
                frameColor = CommonSetting._blueFrameColor;
                break;
            case Element.darkMagic:
                color = CommonSetting._darkBgColor;
                frameColor = CommonSetting._darkFrameColor;
                break;
            case Element.redMagic:
                color = CommonSetting._redBgColor;
                frameColor = CommonSetting._redFrameColor;
                break;
            case Element.lightMagic:
                color = CommonSetting._lightBgColor;
                frameColor = CommonSetting._lightFrameColor;
                break;
            case Element.greenMagic:
                color = CommonSetting._greenBgColor;
                frameColor = CommonSetting._greenFrameColor;
                break;
            default:
                color = CommonSetting._emptyBgColor;
                frameColor = CommonSetting._emptyFrameColor;
                break;
        }
        
        frame.color = frameColor;
        iconBg.color = color;
        icon.sprite = sprite;
        await UniTask.DelayFrame(1);
        AdjustSize(icon);
        icon.gameObject.SetActive(sprite != null);
    }
    
    public static void SelectedFeature(Transform unitIcon, GameObject selectedFrame, float localScale)
    {
        if (unitIcon == null)
        {
            selectedFrame.SetActive(false);
            return;
        }
        selectedFrame.transform.SetParent(unitIcon.transform);
        selectedFrame.transform.localPosition = Vector3.zero;
        selectedFrame.GetComponent<RectTransform>().localPosition = new Vector3(0, 0, 0);
        selectedFrame.GetComponent<RectTransform>().localScale = new Vector3(localScale, localScale, localScale);
        selectedFrame.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        selectedFrame.transform.SetAsFirstSibling();
        selectedFrame.gameObject.SetActive(true);
    }
    
    public static HeroIcon ArrangeHeroIconToParent(HeroIcon prefab, UnitInfo unitInfo, Action<string> iconBehaviour, 
        RectTransform T, float iconSize = 100, bool withSkillCheck = false, bool showIllegalFlag = false)
    {
        var icon = Instantiate(prefab);
        var unitConfig = Units.GetUnitConfig(unitInfo.r_id);
        icon.unitConfig = unitConfig;
        icon.ChangeIcon(unitInfo, withSkillCheck);
        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(iconSize,iconSize);
        icon.transform.SetParent(T);
        icon.transform.localPosition = Vector3.one;
        icon.transform.localScale = Vector3.one;
        icon.warnFlag.SetActive(showIllegalFlag && unitInfo.set.CheckEdit() != SkillSet.SkillEditError.Perfect);
        icon.iconButton.interactable = true;
        icon.iconButton.SetListener(
            () =>
            {
                iconBehaviour(unitInfo.id);
            }
        );
        icon.gameObject.SetActive(true);
        return icon;
    }
    
    public static GameObject dragging;                                          // Icon of dragged item
    public static HeroIcon draggedItem;                                      // Item that is dragged now
    public static HeroCell sourceCell;                                         // From this cell dragged item is
    static Canvas canvas;                                                       // Canvas for item drag operation
    static readonly string canvasName = "DragAndDropCanvas";                    // Name of canvas
    static readonly int canvasSortOrder = 100;                                  // Sort order for canvas
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragging != null)
        {
            DestroyImmediate(dragging);
        }

        sourceCell = GetCell();                       							// Remember source cell
        draggedItem = this;                                                     // Set as dragged item
        
        // version 2
        dragging = Instantiate(this.gameObject);
        dragging.transform.SetParent(canvas.transform);
        dragging.name = "Icon";
        
        var iconRect = dragging.GetComponent<RectTransform>();
        // Set icon's dimensions
        var myRect = GetComponent<RectTransform>();
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(myRect.rect.width, myRect.rect.height);
        //OnItemDragStartEvent?.Invoke(this);                                         // Notify all items about drag start for raycast disabling
    }
    
    /// <summary>
    /// Gets DaD cell which contains this item.
    /// </summary>
    /// <returns>The cell.</returns>
    public HeroCell GetCell()
    {
        return GetComponentInParent<HeroCell>();
    }
    
    /// <summary>
    /// Every frame on this item drag.
    /// </summary>
    /// <param name="data"></param>
    public void OnDrag(PointerEventData data)
    {
        if (dragging != null)
        {
            dragging.transform.position = Input.mousePosition;                          // Item's icon follows to cursor in screen pixels
        }
    }

    /// <summary>
    /// This item is dropped.
    /// </summary>
    /// <param name="eventData"></param>
    /// 这个环节里很多操作看起来和DummyControlUnit里的.DropEventEnd很像，不要被迷惑，真正处理适配技能石功能的主要是DummyControlUnit那边，
    /// 这个环节处理的是把石头从9宫拖出来扔到空白区域的情况。
    /// 这个空白区域应该覆盖技能石头盒子，因为玩家想撤销添加技能操作的时候会本能的把石头向盒子方向移动。
    public void OnEndDrag(PointerEventData eventData)
    {
        ResetConditions();
    }
    
    /// <summary>
    /// Enable item's raycast.
    /// </summary>
    /// <param name="condition"> true - enable, false - disable </param>
    public void MakeRaycast(bool condition)
    {
        if (GetComponent<Image>() != null)
        {
            GetComponent<Image>().raycastTarget = condition;
        }
    }
    
    /// <summary>
    /// Resets all temporary conditions.
    /// </summary>
    void ResetConditions()
    {
        if (dragging != null)
        {
            Destroy(dragging);
        }
        draggedItem = null;
        dragging = null;
        sourceCell = null;
    }
    
    void Awake()
    {
        if (canvas == null)
        {
            var canvasObj = new GameObject(canvasName);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Skill;
using dataAccess;

/// <summary>
/// Drag and Drop item.
/// </summary>
public partial class SKStoneItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	public static SKStoneItem draggedItem;                                      // Item that is dragged now
	public static GameObject dragging;                                          // Icon of dragged item
	public static StoneCell sourceCell;                                         // From this cell dragged item is
    
    static Canvas canvas;                                                       // Canvas for item drag operation
    static readonly string canvasName = "DragAndDropCanvas";                    // Name of canvas
    static readonly int canvasSortOrder = 100;                                  // Sort order for canvas

    public Image image;
    public Text info;
    //自定义item属性
    public SkillConfig _SkillConfig;
    public bool Inherent;
    public string instanceId;
    
    // 指的不是被某个角色装备中，而是指在技能编辑页面等处，被box之外的模块使用中。
    public bool _using = false;
    
    /// <summary>
    /// Awake this instance.
    /// </summary>
    void Awake()
	{
		if (canvas == null)
		{
			var canvasObj = new GameObject(canvasName);
			canvas = canvasObj.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = canvasSortOrder;
			RenderStoneLevel(false);
		}
	}

    public void RenderStoneLevel(bool on)
    {
	    RenderStoneLevel();
	    info.gameObject.SetActive(on);
    }
    
    // 在技能石模型上显示目前等级
    public void RenderStoneLevel()
    {
	    var stoneInfo = Stones.Get(instanceId);
	    var levelText = string.Empty;
	    if (stoneInfo != null)
	    {
		    if (stoneInfo.Born == "true")
		    {
			    levelText = "Adaptive";
		    }
		    else
		    {
			    levelText = stoneInfo.Level == PlayFabSetting._VersionMaxStoneLevel ? "MAX" : stoneInfo.Level.ToString();
		    }
	    }
	    info.text = levelText;
    }
    
	/// <summary>
	/// This item started to drag.
	/// </summary>
	/// <param name="eventData"></param>
	public void OnBeginDrag(PointerEventData eventData)
	{
		if (dragging != null)
			return;
		
        sourceCell = GetCell();                       							// Remember source cell
        draggedItem = this;                                                     // Set as dragged item
                                                                                // Create item's icon
        // version 1
        //icon = new GameObject();
        //icon.transform.SetParent(canvas.transform);
        //icon.name = "Icon";
        //Image myImage = GetComponent<Image>();
        //myImage.raycastTarget = false;                                        	// Disable icon's raycast for correct drop handling
        //Image iconImage = icon.AddComponent<Image>();
        //iconImage.raycastTarget = false;
        //iconImage.sprite = myImage.sprite;
        //iconImage.color = myImage.color;
        
        // version 2
        dragging = Instantiate(this.gameObject);
        transform.GetComponent<Image>().color = new Color(transform.GetComponent<Image>().color.r, transform.GetComponent<Image>().color.g, transform.GetComponent<Image>().color.b,0);
        dragging.transform.SetParent(canvas.transform);
        dragging.name = "Icon";
        var iconImage = dragging.GetComponent<Image>();
        iconImage.raycastTarget = false;
        
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
        transform.GetComponent<Image>().color = new Color(transform.GetComponent<Image>().color.r, transform.GetComponent<Image>().color.g, transform.GetComponent<Image>().color.b,1);
    }

    /// <summary>
    /// Resets all temporary conditions.
    /// </summary>
    void ResetConditions()
    {
        if (dragging != null)
        {
            Destroy(dragging);                                                          // Destroy icon on item drop
        }
        //OnItemDragEndEvent?.Invoke(this);                                               // Notify all cells about item drag end
        draggedItem = null;
        dragging = null;
        sourceCell = null;
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
	/// Gets DaD cell which contains this item.
	/// </summary>
	/// <returns>The cell.</returns>
	public StoneCell GetCell()
	{
		return GetComponentInParent<StoneCell>();
	}

	/// <summary>
	/// Raises the disable event.
	/// </summary>
	void OnDisable()
	{
		ResetConditions();
	}
}

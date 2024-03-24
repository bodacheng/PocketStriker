using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using dataAccess;

/// <summary>
/// Every item's cell must contain this script
/// </summary>

[RequireComponent(typeof(Image))]
public partial class StoneCell : MonoBehaviour, IDropHandler
{
    public enum CellPhase
    {
        SkillStoneBoxCell,
        NineSlotCell,
        SKLevelUpMSlot,
        StoneMergeSlot
    }
    
    public BOButton btn;
    [Tooltip("using Stone Unit Icon")]
    [SerializeField] HeroIcon unitIcon;
    [Tooltip("选中框，用来确保有一个选中框选中这个格子的时候不会有其他选中框选中他。")]
    public GameObject _selected;
    [Tooltip("Functional type of this cell")]
    public CellPhase cellPhase = CellPhase.SkillStoneBoxCell;
    
    SKStoneItem _myDadItem;
    
    /// <summary>
    /// Put item into this cell.(Keep old item in that cell safe)
    /// </summary>
    /// <param name="item">Item.</param>
    void PlaceItemNotDestroyOldItemVersion(SKStoneItem item)
    {
        item.gameObject.SetActive(true);
        RemoveToTemp();
        switch(cellPhase)
        {
            case CellPhase.NineSlotCell:
            case CellPhase.StoneMergeSlot:
            case CellPhase.SKLevelUpMSlot:
                item._using = true;
                break;
            default:
                item._using = false;
                break;
        }
        item.transform.SetParent(transform, false);
        item.transform.SetAsLastSibling();
        item.transform.localScale = Vector3.one;
        var childRectTransform = item.transform.GetComponent<RectTransform>();
        childRectTransform.anchorMin = Vector2.zero; // 设置锚点为左下角
        childRectTransform.anchorMax = Vector2.one; // 设置锚点为右上角
        childRectTransform.offsetMin = Vector2.zero; // 设置左边和下边的边缘
        childRectTransform.offsetMax = Vector2.zero;
        item.transform.localPosition = Vector3.zero;
        item.MakeRaycast(true);
    }
    
    /// <summary>
    /// Updates my item
    /// </summary>
    public void UpdateMyItem()
    {
        _myDadItem = GetComponentInChildren<SKStoneItem>();
        if (cellPhase == CellPhase.SkillStoneBoxCell)
        {
            if (gameObject.activeSelf)
            {
                ShowUsingUnit(_myDadItem, unitIcon);
            }
        }
    }
    
    // Show Character icon using this SkillStone
    void ShowUsingUnit(SKStoneItem item, HeroIcon targetIcon)
    {
        if (item == null || item.instanceId == null)
        {
            targetIcon.gameObject.SetActive(false);
            return;
        }
        var ssInfo = Stones.Get(item.instanceId);
        if (ssInfo == null || ssInfo.unitInstanceId == null)
        {
            targetIcon.gameObject.SetActive(false);
            return;
        }
        
        var unitInfo = dataAccess.Units.Get(ssInfo.unitInstanceId);
        if (unitInfo == null)
        {
            targetIcon.gameObject.SetActive(false);
            return;
        }
        targetIcon.transform.SetAsLastSibling();
        targetIcon.gameObject.SetActive(true);
        targetIcon.ChangeIcon(unitInfo);
    }
    
    /// <summary>
    /// Manualy add item into this cell
    /// </summary>
    /// <param name="newItem"> New item </param>
    public void AddItem(SKStoneItem newItem)
    {
        if (newItem != null)
        {
            PlaceItemNotDestroyOldItemVersion(newItem);//PlaceItem(newItem); 2018.10.9
            UpdateMyItem();
        }
    }
    
    /// <summary>
    /// Swap items between two cells
    /// </summary>
    /// <param name="firstCell"> Cell </param>
    /// <param name="secondCell"> Cell </param>
    public void SwapItems(StoneCell firstCell, StoneCell secondCell)
    {
        if ((firstCell != null) && (secondCell != null))
        {
            firstCell.UpdateMyItem();
            secondCell.UpdateMyItem();
            var firstItem = firstCell.GetItem();                // Get item from first cell
            var secondItem = secondCell.GetItem();              // Get item from second cell

            if (firstItem != null)
            {
                //firstItem.transform.DOMove(secondCell.transform.position,1f);
                //firstItem.transform.localPosition = Vector3.zero;
                //firstItem.MakeRaycast(true);
                secondCell.AddItem(firstItem);
            }
            if (secondItem != null)
            {
                firstCell.AddItem(secondItem);
                secondItem.transform.position = secondCell.transform.position;
                secondItem.transform.DOMove(firstCell.transform.position,0.5f);
            }
        }
    }
    
    /// <summary>
    /// Get item from this cell
    /// </summary>
    /// <returns> Item </returns>
    public SKStoneItem GetItem()
    {
        UpdateMyItem();
        return _myDadItem;
    }
}

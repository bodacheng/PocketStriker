using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class HeroCell : MonoBehaviour, IDropHandler
{
    public BOButton iconButton;
    public Action<string> TeamEdit;
    public Action<string> sourceCellSwapAction;
    private HeroIcon contained;

    /// <summary>
    /// Item is dropped in this cell
    /// </summary>
    /// <param name="data"></param>
    public void OnDrop(PointerEventData data)
    {
        var sourceCell = HeroIcon.sourceCell;
        if (HeroIcon.dragging != null)
        {
            var item = HeroIcon.draggedItem;
            if (item == null)
                return;
            if (sourceCell == this)
                return;

            string formerInstanceId = null;
            if (contained != null)
            {
                if (contained.InstanceID != null)
                {
                    formerInstanceId = contained.InstanceID;
                }
            }
            
            if (HeroIcon.dragging.activeSelf)
            {
                TeamEdit.Invoke(HeroIcon.draggedItem.InstanceID);
                sourceCell?.sourceCellSwapAction?.Invoke(formerInstanceId);
            }
            
            // 以下代码是由于无法解释的bug下无奈之举
            if (sourceCell != null)
            {
                DestroyImmediate(HeroIcon.dragging);
            }
        }
    }
    
    /// <summary>
    /// Manualy add item into this cell
    /// </summary>
    /// <param name="newItem"> New item </param>
    public void AddItem(HeroIcon newItem)
    {
        if (newItem != null)
        {
            PlaceItem(newItem);
        }
    }
    
    /// <summary>
    /// Put item into this cell.(Keep old item in that cell safe)
    /// </summary>
    /// <param name="item">Item.</param>
    void PlaceItem(HeroIcon item)
    {
        if (contained != null && contained != item)
            DestroyImmediate(contained.gameObject);
        
        contained = item;
        contained.gameObject.SetActive(true);
        contained.transform.SetParent(transform, false);
        contained.transform.SetAsLastSibling();
        contained.transform.localScale = Vector3.one;
        contained.transform.GetComponent<RectTransform>().sizeDelta 
            = transform.GetComponent<RectTransform>().sizeDelta;
        contained.transform.localPosition = Vector3.zero;
        contained.MakeRaycast(true);
    }

    public void Clear()
    {
        if (contained != null)
        {
            DestroyImmediate(contained.gameObject);
        }
    }
}

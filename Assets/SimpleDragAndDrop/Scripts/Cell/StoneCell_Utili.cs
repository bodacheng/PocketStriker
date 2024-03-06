using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Every item's cell must contain this script
/// </summary>

[RequireComponent(typeof(Image))]
public partial class StoneCell : MonoBehaviour, IDropHandler
{
    public static void SelectedRender(StoneCell cell, GameObject _selected)
    {
        if (cell == null)
        {
            _selected.SetActive(false);
            return;
        }
        _selected.SetActive(true);
        _selected.transform.SetParent(cell.GetComponent<RectTransform>());
        _selected.transform.localPosition = Vector3.zero;

        var rect = _selected.GetComponent<RectTransform>();
        
        rect.localPosition = new Vector3(0, 0, 0);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector3.zero;
        _selected.transform.SetAsFirstSibling();
    }
}

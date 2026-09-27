using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DropTableInfoLayer : UILayer
{
    [SerializeField] ResultTableNode prefab;
    [SerializeField] VerticalLayoutGroup resultT;
    [SerializeField] RectTransform viewPortRect;
    readonly List<ResultTableNode> rows = new List<ResultTableNode>();
    float originalViewPortHeight;
    
    public void ShowDropTableInfo(CloudScriptRandomResultTableListing tableInfo)
    {
        if (this == null)
            return;

        foreach (var row in rows)
        {
            if (row == null)
                continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        rows.Clear();

        var nodes = tableInfo?.Nodes?.Where(node => node != null && node.Weight >= 0 && !string.IsNullOrEmpty(node.ResultItem))
            .OrderBy(node => node.Weight)
            .ToList() ?? new List<CloudScriptResultTableNode>();
        var wholeWeight = nodes.Sum(node => (long)node.Weight);
        float rectHeight = resultT.padding.vertical;
        float itemHeight = 0;

        foreach (var node in nodes)
        {
            var nodeUI = Instantiate(prefab, resultT.transform, false);
            rows.Add(nodeUI);
            nodeUI.Setup(node.ResultItem, wholeWeight > 0 ? (double)node.Weight / wholeWeight : 0);
            nodeUI.gameObject.SetActive(true);
            nodeUI.transform.localScale = Vector3.one;
            itemHeight = nodeUI.GetComponent<RectTransform>().rect.height;
            rectHeight += itemHeight;
        }

        rectHeight += Mathf.Max(0, rows.Count - 1) * resultT.spacing;
        resultT.GetComponent<RectTransform>().sizeDelta =
            new Vector2(resultT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);

        if (viewPortRect != null && itemHeight > 0 && itemHeight + resultT.spacing > 0)
        {
            if (originalViewPortHeight <= 0)
                originalViewPortHeight = viewPortRect.rect.height;
            var adjustedHeight = PosCal.AdjustedViewPortHeight(originalViewPortHeight, itemHeight, resultT.spacing);
            if (adjustedHeight > 0)
                viewPortRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, adjustedHeight);
        }
    }
}

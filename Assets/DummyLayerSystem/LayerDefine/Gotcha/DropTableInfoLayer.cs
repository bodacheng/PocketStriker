using System.Linq;
using PlayFab.ServerModels;
using UnityEngine;
using UnityEngine.UI;

public class DropTableInfoLayer : UILayer
{
    [SerializeField] ResultTableNode prefab;
    [SerializeField] VerticalLayoutGroup resultT;
    [SerializeField] RectTransform viewPortRect;
    
    public void ShowDropTableInfo(RandomResultTableListing tableInfo)
    {
        float rectHeight = 0;
        var wholeWeight = 0;
        
        tableInfo.Nodes = tableInfo.Nodes.OrderBy(x=> x.Weight).ToList();
        
        for (var i = 0; i < tableInfo.Nodes.Count; i++)
        {
            var node = tableInfo.Nodes[i];
            wholeWeight += node.Weight;
        }

        float itemHeight = -1;
        foreach (var node in tableInfo.Nodes)
        {
            var nodeUI = Instantiate(prefab);
            nodeUI.Setup(node.ResultItem, (double) node.Weight / wholeWeight);
            nodeUI.gameObject.transform.SetParent(resultT.transform);
            nodeUI.gameObject.SetActive(true);
            nodeUI.transform.localScale = Vector3.one;
            if (itemHeight < 0)
                itemHeight = nodeUI.GetComponent<RectTransform>().rect.height;
            rectHeight += (itemHeight + resultT.spacing);
        }
        
        rectHeight -= resultT.spacing;
        resultT.GetComponent<RectTransform>().sizeDelta = new Vector2(resultT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);
        viewPortRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 
            PosCal.AdjustedViewPortHeight(viewPortRect.rect.height, itemHeight, resultT.spacing));
    }
}


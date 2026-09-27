using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DropTableInfoLayer : UILayer
{
    [SerializeField] ResultTableNode prefab;
    [SerializeField] VerticalLayoutGroup resultT;
    
    public void ShowDropTableInfo(CloudScriptRandomResultTableListing tableInfo)
    {
        float rectHeight = 0;
        var wholeWeight = 0;
        
        tableInfo.Nodes = tableInfo.Nodes.OrderBy(x=> x.Weight).ToList();
        
        for (var i = 0; i < tableInfo.Nodes.Count; i++)
        {
            var node = tableInfo.Nodes[i];
            wholeWeight += node.Weight;
        }
        
        foreach (var node in tableInfo.Nodes)
        {
            var nodeUI = Instantiate(prefab);
            nodeUI.Setup(node.ResultItem, (double) node.Weight / wholeWeight);
            nodeUI.gameObject.transform.SetParent(resultT.transform);
            nodeUI.transform.localScale = Vector3.one;
            rectHeight += (nodeUI.GetComponent<RectTransform>().rect.height + resultT.spacing);
        }
        resultT.GetComponent<RectTransform>().sizeDelta = 
            new Vector2(resultT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);
    }
}

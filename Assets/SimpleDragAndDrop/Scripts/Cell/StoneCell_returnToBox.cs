using UnityEngine;
using UnityEngine.EventSystems;
using mainMenu;

public partial class StoneCell : MonoBehaviour, IDropHandler
{
    public void RemoveToTemp()
    {
        UpdateMyItem();
        if (_myDadItem)
        {
            _myDadItem._using = false;
            _myDadItem.gameObject.transform.SetParent(PreScene.target.stonesTempContainer);
        }
        UpdateMyItem();
    }
}

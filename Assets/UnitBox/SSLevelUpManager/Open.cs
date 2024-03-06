using dataAccess;
using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public partial class SSLevelUpManager : MonoBehaviour
{
    public void OpenLevelUpPage()
    {
        var renderModel = Stones.GetRenderModel(_stoneListLayer.TargetStoneID);
        renderModel._using = true;
        
        var layer = UILayerLoader.Get<StoneListLayer>();
        layer.TargetStoneID = _stoneListLayer.TargetStoneID;
        layer.box.AddFeatureToCells(layer.CellFeature_MAdd);
        RefreshSkillLevelUpModule(_stoneListLayer.TargetStoneID);
        Stones.HighLight(renderModel._SkillConfig.RECORD_ID);
        gameObject.SetActive(true);
    }
    
    void CloseLevelUpPage()
    {
        if (!gameObject.activeSelf)
            return;
        var layer = UILayerLoader.Get<StoneListLayer>();
        var renderModel = Stones.GetRenderModel(_stoneListLayer.TargetStoneID);
        if (renderModel == null)
        {
            Debug.Log("Logic Error:"+ _stoneListLayer.TargetStoneID);
            return;
        }
        
        renderModel._using = false;
        SKStoneItem.SelectedRender(renderModel, SkillStonesBox.Selected);
        _stoneListLayer.TargetStoneID = renderModel.instanceId;
        
        foreach (var t in _materialSlots)
        {
            if (t.GetItem() != null)
            {
                layer.box.ReturnStoneToBox(t.GetItem());
            }
        }
        
        layer.box.AddFeatureToCells(layer.CellFeature_StoneShow);
        RefreshSkillLevelUpModule(_stoneListLayer.TargetStoneID);
        Stones.ResetHighLight();
        gameObject.SetActive(false);
    }
}

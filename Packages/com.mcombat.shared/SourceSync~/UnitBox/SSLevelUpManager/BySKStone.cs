using UnityEngine;
using dataAccess;
using System.Collections.Generic;

// 技能石消耗
public partial class SSLevelUpManager : MonoBehaviour
{
    List<StoneCell> _materialSlots;
    
    public void AddMaterialFromCell(StoneCell boxCell)
    {
        var target = Stones.Get(_stoneListLayer.TargetStoneID);
        if (target == null)
        {
            return;
        }
        
        foreach (var slot in _materialSlots)
        {
            var material = boxCell.GetItem();
            if (slot.GetItem() == null && material != null && material._SkillConfig.RECORD_ID == target.SkillId) // 只能以同技能石为材料
            {
                if (material.instanceId != target.InstanceId)
                {
                    StoneCell.Install(boxCell, slot);
                }
            }
        }
    }
    
    // 自动添加技能卡
    void AutoAddMaterials(string skillId)
    {
        var stones = Stones.GetMyStonesBySkillID(skillId);
        var slotIndex = 0;
        while (slotIndex < _materialSlots.Count)
        {
            if (stones.Count == 0)
            {
                break;
            }
            var alreadyThere = _materialSlots[slotIndex].GetItem();
            if (alreadyThere == null)
            {
                var stoneInstanceID = stones[stones.Count-1];
                var itemModel = Stones.GetRenderModel(stoneInstanceID);
                var itemData = Stones.Get(stoneInstanceID);
                if (_stoneListLayer.TargetStoneID != stoneInstanceID && dataAccess.Units.Get(itemData.unitInstanceId) == null)
                {
                    _materialSlots[slotIndex].AddItem(itemModel);
                    slotIndex += 1;
                }
                stones.Remove(stoneInstanceID);
            }
            else
            {
                stones.Remove(alreadyThere.instanceId);
                slotIndex += 1;
            }
        }
        RefreshSkillLevelUpModule(_stoneListLayer.TargetStoneID);
    }
}
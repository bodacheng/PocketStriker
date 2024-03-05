using mainMenu;
using dataAccess;
using UnityEngine;
using System.Linq;

public partial class SkillEditLayer : UILayer
{
    void FinishRemains()
    {
        var info = PreScene.target.Focusing;
        var unitConfig = Units.GetUnitConfig(info.r_id);
        var now = nineSlot.GetCurrentNineAndTwo();
        var targetSkillSet = SkillSet.FixSkillSet(unitConfig.TYPE, now,  true);
        
        if (targetSkillSet == null)
        {
            // 这里必须有某些其他处理（比如不让按钮显示？）
            PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughStoneToFill"));
        }
        else
        {
            Finish(info, targetSkillSet);
        }
    }
    
    void RandomAll()
    {
        var info = PreScene.target.Focusing;
        var unitConfig = Units.GetUnitConfig(info.r_id);
        var originSkillInfo = Stones.GetOriginSkillOfUnit(info.id);
        // 这一步仅仅是根据账户拥有技能石的情况来确定了可行的技能组，也就是说根据手上的石头这个技能组能拼出来，但没提供具体的石头，所以防重复工作在实际装备技能石的时候（AddRandomStoneToSlot）也要做
        var targetSkillSet = SkillSet.RandomSkillSet(unitConfig.TYPE, originSkillInfo?.SkillId, true);
        ForceClearAll();
        Finish(info, targetSkillSet);
    }

    void Finish(UnitInfo info, SkillSet targetSkillSet)
    {
        AddRandomStoneToSlot(info.id, 1, targetSkillSet.a1);
        AddRandomStoneToSlot(info.id, 2, targetSkillSet.a2);
        AddRandomStoneToSlot(info.id, 3, targetSkillSet.a3);
        AddRandomStoneToSlot(info.id, 4, targetSkillSet.b1);
        AddRandomStoneToSlot(info.id, 5, targetSkillSet.b2);
        AddRandomStoneToSlot(info.id, 6, targetSkillSet.b3);
        AddRandomStoneToSlot(info.id, 7, targetSkillSet.c1);
        AddRandomStoneToSlot(info.id, 8, targetSkillSet.c2);
        AddRandomStoneToSlot(info.id, 9, targetSkillSet.c3);
        nineSlot.NineSlotsStatusRefresh();
        stonesBox.RestFilter();
    }
    
    void AddRandomStoneToSlot(string unitInstanceId, int targetSlot, string skillID)
    {
        if (nineSlot.AllSlot[targetSlot - 1]._cell.GetItem() != null)
        {
            return;
        }
        
        var originSkillInfo = Stones.GetOriginSkillOfUnit(unitInstanceId);
        var options = Stones.GetMyStonesBySkillID(skillID);
        if (originSkillInfo != null && skillID == originSkillInfo.SkillId)
        {
            nineSlot.AllSlot[targetSlot - 1]._cell.AddItem(Stones.GetRenderModel(originSkillInfo.InstanceId));
        }else{
            options = options.OrderByDescending(x => Stones.Get(x).Level).ToList();
            string targetStoneId = null;
            for (int i = 0 ; i < options.Count; i++)
            {
                var stoneInfo = Stones.Get(options[i]);
                if ((unitInstanceId != stoneInfo.unitInstanceId && dataAccess.Units.Get(stoneInfo.unitInstanceId) == null)
                    ||
                    unitInstanceId == stoneInfo.unitInstanceId)
                {
                    targetStoneId = options[i];
                    break;
                }
            }
            nineSlot.AllSlot[targetSlot - 1]._cell.AddItem(Stones.GetRenderModel(targetStoneId));
        }
        
        var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillID);
        stonesBox._tabEffects.SkillButtonExplosion(skillConfig.SP_LEVEL,
            PosCal.GetWorldPos(PreScene.target.postProcessCamera, 
                nineSlot.AllSlot[targetSlot - 1]._cell.GetComponent<RectTransform>(), 
                3),
            stonesBox._tabEffects.transform);
    }
}

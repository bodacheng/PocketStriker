using UnityEngine;
using dataAccess;
using DG.Tweening;
using DummyLayerSystem;
using mainMenu;

public static class SVCenter
{
    public static void StoneRemoveFromSlotToCell(StoneCell sourceCell, StoneCell boxcell)
    {
        if (sourceCell.cellPhase == StoneCell.CellPhase.NineSlotCell)
        {
            var stone = sourceCell.GetItem();
            if (stone != null && stone.Inherent)
            {
                PopupLayer.ArrangeWarnWindow(Translate.Get("BornSkillCantRemove"));
                return;
            }
            
            var layer = UILayerLoader.Get<SkillEditLayer>();
            // 如果把技能石从9宫格拖到技能背包的一个有石头的格子上，那么就直接把拖动中的技能石先从九宫格拔下来，接着让技能背包自动排序一下
            if (boxcell.GetItem() != null)
            {
                layer.stonesBox.ReturnStoneToBox(stone);
            }
            else
            {
                // 如果把技能石从9宫格拖到空技能背包格子上，那就让这个技能石在那个空格子上就可以。
                // 的确这个瞬间可能产生这个技能石所在位置和当前背包显示类型不一致问题，但如果是进行了一个背包自动排序的话，
                // 松手瞬间会有一个技能石“变图案”的错觉。
                boxcell.AddItem(stone);
            }
            layer.nineSlot.NineSlotsStatusRefresh();
        }
        else if (sourceCell.cellPhase == StoneCell.CellPhase.SKLevelUpMSlot)
        {
            var stone = sourceCell.GetItem();
            var sl = UILayerLoader.Get<StoneListLayer>();
            if (boxcell.GetItem() != null)
            {
                sl.box.ReturnStoneToBox(stone);
            }            
            else
            {
                boxcell.AddItem(stone);
            }
            sl.levelManager.RefreshSkillLevelUpModule(sl.TargetStoneID);
        }
    }
    
    public static void MoveItemFromTo(StoneCell from, StoneCell to)
    {
        var item = from.GetItem();
        if (item == null)
            return;
        
        if (to.cellPhase == StoneCell.CellPhase.NineSlotCell && from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            var info = Stones.Get(item.instanceId);
            var unitInfo = dataAccess.Units.Get(info.unitInstanceId);
            if (unitInfo != null && unitInfo.id != PreScene.target.Focusing.id)
            {
                PopupLayer.ArrangeWarnWindowUnitIcon(Translate.Get("OtherUnitUsing"), dataAccess.Units.Get(info.unitInstanceId).r_id);
                return;
            }

            var layer = UILayerLoader.Get<SkillEditLayer>();
            
            var currentSkillIds = layer.nineSlot.GetCurrentNineSlotAllSkillIds();
            if (currentSkillIds.Contains(item._SkillConfig.RECORD_ID))
            {
                // 不可出现相同技能
                PopupLayer.ArrangeWarnWindow(Translate.Get("CantEquipSameSkill"));
                return;
            }
            layer.stonesBox._tabEffects.SkillButtonExplosion(item._SkillConfig.SP_LEVEL, 
                PosCal.GetWorldPos(PreScene.target.postProcessCamera, to.GetComponent<RectTransform>(), 3), 
                layer.stonesBox._tabEffects.transform);
        }
        
        to.AddItem(item);
        from.UpdateMyItem();
        
        if (from.cellPhase == StoneCell.CellPhase.NineSlotCell || to.cellPhase == StoneCell.CellPhase.NineSlotCell)
        {
            var layer = UILayerLoader.Get<SkillEditLayer>();
            layer.nineSlot.NineSlotsStatusRefresh();
        }
        
        if (from.cellPhase == StoneCell.CellPhase.SKLevelUpMSlot || to.cellPhase == StoneCell.CellPhase.SKLevelUpMSlot)
        {
            var sl = UILayerLoader.Get<StoneListLayer>();
            sl.levelManager.RefreshSkillLevelUpModule(sl.TargetStoneID);
        }
    }
    
    public static void SwapItemFromTo(StoneCell from, StoneCell to)
    {
        var fromItem = from.GetItem();
        if (fromItem == null)
            return;
        
        var toItem = to.GetItem();
        
        if (to.cellPhase == StoneCell.CellPhase.NineSlotCell && from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            var info = Stones.Get(fromItem.instanceId);
            var unitInfo = dataAccess.Units.Get(info.unitInstanceId);
            if (unitInfo != null && unitInfo.id != PreScene.target.Focusing.id)
            {
                PopupLayer.ArrangeWarnWindowUnitIcon(Translate.Get("OtherUnitUsing"), unitInfo.r_id);
                return;
            }
            
            var skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
            var currentSkillIds = skillEditLayer.nineSlot.GetCurrentNineSlotAllSkillIds();
            
            if (toItem != null)
            {
                if (toItem._SkillConfig.RECORD_ID != fromItem._SkillConfig.RECORD_ID)
                {
                    if (currentSkillIds.Contains(fromItem._SkillConfig.RECORD_ID))
                    {
                        // 不可出现相同技能
                        PopupLayer.ArrangeWarnWindow(Translate.Get("CantEquipSameSkill"));
                        return;
                    }
                }

                if (toItem.Inherent)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("BornSkillCantRemove"));
                    return;
                }
            }
            
            skillEditLayer.stonesBox._tabEffects.SkillButtonExplosion(fromItem._SkillConfig.SP_LEVEL, 
            PosCal.GetWorldPos(PreScene.target.postProcessCamera, to.GetComponent<RectTransform>(), 3), 
            skillEditLayer.stonesBox._tabEffects.transform);
        }
        
        // 把技能石从技能槽拖回技能石盒，如果是固有技能石，连移动也不允许
        if (to.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell && from.cellPhase == StoneCell.CellPhase.NineSlotCell)
        {
            var stone = to.GetItem();
            if (stone.Inherent)
            {
                PopupLayer.ArrangeWarnWindow(Translate.Get("BornSkillCantRemove"));
                return;
            }
        }
        
        SwapItems(from, to);
        
        if (from.cellPhase == StoneCell.CellPhase.NineSlotCell || to.cellPhase == StoneCell.CellPhase.NineSlotCell)
        {
            var skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
            skillEditLayer.nineSlot.NineSlotsStatusRefresh();
        }
        
        if (from.cellPhase == StoneCell.CellPhase.SKLevelUpMSlot || to.cellPhase == StoneCell.CellPhase.SKLevelUpMSlot)
        {
            var sl = UILayerLoader.Get<StoneListLayer>();
            sl.levelManager.RefreshSkillLevelUpModule(sl.TargetStoneID);
        }
    }
    
    /// <summary>
    /// Swap items between two cells
    /// </summary>
    /// <param name="firstCell"> Cell </param>
    /// <param name="secondCell"> Cell </param>
    static void SwapItems(StoneCell firstCell, StoneCell secondCell)
    {
        firstCell.UpdateMyItem();
        secondCell.UpdateMyItem();
        SKStoneItem firstItem = firstCell.GetItem();                // Get item from first cell
        SKStoneItem secondItem = secondCell.GetItem();              // Get item from second cell
        // Swap items
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
            secondItem.transform.DOMove(firstCell.transform.position,0.5f).OnComplete(() =>
            {
                secondItem.transform.localPosition = Vector3.zero;
            });
        }
    }
    
    // old 尝试装载的技能石正被其他角色使用时候，对那个其他角色进行validation检验
    static bool CheckIfOtherUnitOkAfterStoneRemove(SKStoneItem item)
    {
        var skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
        if (item.Inherent)
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("BornSkillCantRemove"));
            return false;
        }
        if (dataAccess.Units.CheckExist(Stones.Get(item.instanceId).unitInstanceId))
        {
            var unitInstanceID = Stones.Get(item.instanceId).unitInstanceId;
            var valR = skillEditLayer.nineSlot.CheckEditAfterOneStoneRemoved(unitInstanceID, item._SkillConfig.RECORD_ID);
            if (valR != SkillSet.SkillEditError.Perfect)
            {
                skillEditLayer.nineSlot.ValidationWarn(valR);
                return false;
            }
        }
        return true;
    }
}

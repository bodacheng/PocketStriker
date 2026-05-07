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
            layer.stonesBox.PlaceStoneFromSlot(stone);
            PlayDropSe();
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
                ApplyTempUnitUsage(stone, boxcell);
                boxcell.AddItem(stone);
                PlayDropSe();
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
                PosCal.GetWorldPos(PreScene.target.noPostProcessCamera, to.GetComponent<RectTransform>(), 3),
                layer.stonesBox._tabEffects.transform);
        }
        
        ApplyTempUnitUsage(item, to);
        to.AddItem(item);
        PlayDropSe();
        from.UpdateMyItem();
        if (from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            from.ClearUsingUnitIcon();
        }
        
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
        
        var skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
        if (to.cellPhase == StoneCell.CellPhase.NineSlotCell && from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            var info = Stones.Get(fromItem.instanceId);
            var unitInfo = dataAccess.Units.Get(info.unitInstanceId);
            if (unitInfo != null && unitInfo.id != PreScene.target.Focusing.id)
            {
                PopupLayer.ArrangeWarnWindowUnitIcon(Translate.Get("OtherUnitUsing"), unitInfo.r_id);
                return;
            }
            
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
            PosCal.GetWorldPos(PreScene.target.noPostProcessCamera, to.GetComponent<RectTransform>(), 3),
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

        // 特殊处理：从盒子拖到有石头的技能槽时，原槽石头按EX页自动归位并滚动显示
        if (to.cellPhase == StoneCell.CellPhase.NineSlotCell && from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell && toItem != null)
        {
            ApplyTempUnitUsage(fromItem, to);
            to.AddItem(fromItem);
            PlayDropSe();
            from.RemoveToTemp();
            skillEditLayer?.stonesBox.PlaceStoneFromSlot(toItem);
            from.ClearUsingUnitIcon();

            skillEditLayer?.nineSlot.NineSlotsStatusRefresh();
            return;
        }

        SwapItems(from, to);
        if (from.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            from.ClearUsingUnitIcon();
        }
        if (to.cellPhase == StoneCell.CellPhase.SkillStoneBoxCell)
        {
            to.ClearUsingUnitIcon();
        }
        
        if (from.cellPhase == StoneCell.CellPhase.NineSlotCell || to.cellPhase == StoneCell.CellPhase.NineSlotCell)
        {
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
            ApplyTempUnitUsage(firstItem, secondCell);
            //firstItem.transform.DOMove(secondCell.transform.position,1f);
            //firstItem.transform.localPosition = Vector3.zero;
            //firstItem.MakeRaycast(true);
            secondCell.AddItem(firstItem);
            PlayDropSe();
        }
        if (secondItem != null)
        {
            ApplyTempUnitUsage(secondItem, firstCell);
            firstCell.AddItem(secondItem);
            secondItem.transform.position = secondCell.transform.position;
            secondItem.transform.DOMove(firstCell.transform.position,0.5f).OnComplete(() =>
            {
                secondItem.transform.localPosition = Vector3.zero;
            });
            PlayDropSe();
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

    static void ApplyTempUnitUsage(SKStoneItem item, StoneCell targetCell)
    {
        if (item == null || targetCell == null)
            return;
        switch (targetCell.cellPhase)
        {
            case StoneCell.CellPhase.NineSlotCell:
                Stones.SetTempUnitUsage(item.instanceId, PreScene.target?.Focusing?.id);
                break;
            case StoneCell.CellPhase.SkillStoneBoxCell:
                break;
        }
    }

    public static void PlayDropSe()
    {
        if (AppSetting.UiAudioSource == null || CommonSetting.BtnTapSound == null)
            return;
        AppSetting.UiAudioSource.volume = AppSetting.Value.EffectsVolume;
        AppSetting.UiAudioSource.PlayOneShot(CommonSetting.BtnTapSound);
    }
}

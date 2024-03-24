using System;
using System.Collections.Generic;
using dataAccess;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

public partial class SSLevelUpManager : MonoBehaviour
{
    [SerializeField] BOButton cancelBtn;
    [SerializeField] BOButton autoAdd;
    [SerializeField] BOButton confirmLevelUp;
    [SerializeField] Text gdCount;
    
    [Header("融合技能槽")]
    [SerializeField] StoneCell cell1;
    [SerializeField] StoneCell cell2;
    [SerializeField] StoneCell cell3;
    [SerializeField] StoneCell cell4;

    [SerializeField] BOButton levelUpAllStonesBtn;
    [SerializeField] Animator levelUpAllStonesBtnAnimator;
    [SerializeField] StoneListLayer _stoneListLayer;

    public BOButton LevelUpAllStonesBtn => levelUpAllStonesBtn;
    public Animator LevelUpAllStonesBtnAnimator => levelUpAllStonesBtnAnimator;

    public void INI()
    {
        cancelBtn.onClick.AddListener(CloseLevelUpPage);
        autoAdd.onClick.AddListener(() =>
        {
            var info = Stones.Get(_stoneListLayer.TargetStoneID);
            AutoAddMaterials(info.SkillId);
        });
        
        _materialSlots = new List<StoneCell>
        {
            cell1,
            cell2,
            cell3,
            cell4
        };
        
        foreach (var cell in _materialSlots)
        {
            cell.SetOnDropAction(MSlotOnDropAction);
        }
        
        levelUpAllStonesBtn.SetListener(
            () =>
            {
                PreScene.target.trySwitchToStep(MainSceneStep.StoneUpdateConfirm);
            });
    }
    
    void MSlotOnDropAction(StoneCell source, StoneCell to)
    {
        if (SKStoneItem.dragging != null)
        {
            var item = SKStoneItem.draggedItem;
            if (item == null)
                return;
            var target = Stones.Get(_stoneListLayer.TargetStoneID);
            if (item.instanceId != _stoneListLayer.TargetStoneID && item._SkillConfig.RECORD_ID == target.SkillId)
            {
                var m = Stones.Get(item.instanceId);
                if (m.Born == "true")
                {
                    PopupLayer.ArrangeWarnWindow("这个是被动技能，不能用作材料");
                    return;
                }
                if (m.unitInstanceId != null)
                {
                    PopupLayer.ArrangeWarnWindow("このストーンは装備中です");
                    return;
                }
                
                StoneCell.Install(source, to);
            }
        }
    }
    
    /// <summary>
    /// 技能石升级画面更新。
    /// </summary>
    public void RefreshSkillLevelUpModule(string instanceId)
    {
        confirmLevelUp.gameObject.SetActive(false);
        if (instanceId == null)
        {
            return;
        }
        var target = Stones.Get(instanceId);
        foreach (var cell in _materialSlots)
        {
            cell.UpdateMyItem();
        }
        foreach (var slot in _materialSlots)
        {
            if (slot.GetItem() == null)
                return; // 材料槽满的时候才可能弹出确认按钮
        }
        
        confirmLevelUp.gameObject.SetActive(true);
        var needGD = 10;
        gdCount.text = needGD.ToString();
        confirmLevelUp.SetListener(
            () =>
            {
                Confirm(instanceId, needGD);
            }
        );
    }
    
    void Confirm(string instanceId, int needGD)
    {
        if (Currencies.CoinCount.Value < needGD)
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughGD"));
            return;
        }
            
        List<string> mInstanceIds = new List<string>();
        var item1 = cell1.GetItem();
        var item2 = cell2.GetItem();
        var item3 = cell3.GetItem();
        var item4 = cell4.GetItem();
        if (item1 != null)
            mInstanceIds.Add(item1.instanceId);
        if (item2 != null)
            mInstanceIds.Add(item2.instanceId);
        if (item3 != null)
            mInstanceIds.Add(item3.instanceId);
        if (item4 != null)
            mInstanceIds.Add(item4.instanceId);
            
        PopupLayer.ArrangeConfirmWindow(
            ()=>
            {
                StoneLevelUpProccessor.LevelUpStone(instanceId, mInstanceIds,
                    x =>
                    {
                        StoneLevelUpProccessor.CalUpdateAllForms();
                        LevelUpAllStonesBtn.interactable = StoneLevelUpProccessor.HasStoneToBeUpdate();
                        LevelUpAllStonesBtnAnimator.SetBool("on", StoneLevelUpProccessor.HasStoneToBeUpdate());
                        // 具体待定。但不应该是RefreshSkillLevelUpModule，这个在CloseLevelUpPage会跑一次才对
                    });
            }, 
            Translate.Get("IfStoneLevelUp"));
    }
}
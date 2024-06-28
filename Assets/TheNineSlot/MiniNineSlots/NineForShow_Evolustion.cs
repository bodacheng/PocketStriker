using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class NineForShow : MonoBehaviour
{
    private Color gray = new Color(0.5f, 0.5f, 0.5f);
    
    public void ClickTargetSlot(int slot)
    {
        BOButton target = null;
        switch (slot)
        {
            case 1:
                target = A1T;
                break;
            case 2:
                target = A2T;
                break;
            case 3: 
                target = A3T;
                break;
            case 4:
                target = B1T;
                break;
            case 5:
                target = B2T;
                break;
            case 6:
                target = B3T;
                break;
            case 7:
                target = C1T;
                break;
            case 8:
                target = C2T;
                break;
            case 9: 
                target = C3T;
                break;
        }
        target.onClick.Invoke();
    }

    public BOButton GetClickedSlot()
    {
        switch (_clickedSlot)
        {
            case 1:
                return A1T;
            case 2:
                return A2T;
            case 3:
                return A3T;
            case 4:
                return B1T;
            case 5:
                return B2T;
            case 6:
                return B3T;
            case 7:
                return C1T;
            case 8:
                return C2T;
            case 9:
                return C3T;
        }
        return null;
    }
    
    public void AddOnClickToSlots(Action<BOButton> onClickStone)
    {
        A1T.SetListener(() =>
        {
            _clickedSlot = 1;
            onClickStone(A1T);
        });
        A2T.SetListener(() =>
        {
            _clickedSlot = 2;
            onClickStone(A2T);
        });
        A3T.SetListener(() =>
        {
            _clickedSlot = 3;
            onClickStone(A3T);
        });
        
        B1T.SetListener(() =>
        {
            _clickedSlot = 4;
            onClickStone(B1T);
        });
        B2T.SetListener(() =>
        {
            _clickedSlot = 5;
            onClickStone(B2T);
        });
        B3T.SetListener(() =>
        {
            _clickedSlot = 6;
            onClickStone(B3T);
        });
        
        C1T.SetListener(() =>
        {
            _clickedSlot = 7;
            onClickStone(C1T);
        });
        C2T.SetListener(() =>
        {
            _clickedSlot = 8;
            onClickStone(C2T);
        });
        C3T.SetListener(() =>
        {
            _clickedSlot = 9;
            onClickStone(C3T);
        });
    }

    public void EvolutionModeSlotInteractiveRefresh(SkillSet set, bool mugen = false)
    {
        // 第一列技能必须有普通技能
        var normalSkillsOfAList = new List<string>();
        var skillConfigA1 = SkillConfigTable.GetSkillConfigByRecordId(set.a1);
        var skillConfigB1 = SkillConfigTable.GetSkillConfigByRecordId(set.b1);
        var skillConfigC1 = SkillConfigTable.GetSkillConfigByRecordId(set.c1);
        
        if (skillConfigA1 is { SP_LEVEL: 0 })
            normalSkillsOfAList.Add(skillConfigA1.REAL_NAME);
        if (skillConfigB1 is { SP_LEVEL: 0 })
            normalSkillsOfAList.Add(skillConfigB1.REAL_NAME);
        if (skillConfigC1 is { SP_LEVEL: 0 })
            normalSkillsOfAList.Add(skillConfigC1.REAL_NAME);
        
        if (!mugen && normalSkillsOfAList.Count == 1)
        {
            if (skillConfigA1 is { SP_LEVEL: 0 })
            {
                A1T.interactable = false;
                var item = A1T.transform.GetComponentInChildren<SKStoneItem>();
                item.image.color = gray;
            }
            if (skillConfigB1 is { SP_LEVEL: 0 })
            {
                B1T.interactable = false;
                var item = B1T.transform.GetComponentInChildren<SKStoneItem>();
                item.image.color = gray;
            }
            if (skillConfigC1 is { SP_LEVEL: 0 })
            {
                C1T.interactable = false;
                var item = C1T.transform.GetComponentInChildren<SKStoneItem>();
                item.image.color = gray;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public partial class SkillEditLayer : UILayer
{
    [Header("Tutorial")] 
    [SerializeField] GameObject spStoneGuide1, spStoneGuide2, spStoneGuide3, spStoneGuide4, spStoneGuide5, spStoneGuide6;
    [SerializeField] ClickNextTutorial clickNextTutorial1, clickNextTutorial2;
    [SerializeField] GameObject clickAutoEditIndicator, clickAutoEditIndicator2;
    [SerializeField] GameObject mask;
    int equippedGuideStep = -1;
    
    bool HasTargetSpLevelStone(int[] targetSpLevels)
    {
        List<int> targetList = targetSpLevels.ToList();
        var skillIds = nineSlot.GetCurrentNineSlotAllSkillIds();
        foreach (var skillId in skillIds)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillId);
            if (skillConfig == null) continue;
            
            if (targetList.Contains(skillConfig.SP_LEVEL))
                targetList.Remove(skillConfig.SP_LEVEL);
        }
        return targetList.Count == 0;
    }

    int FullSpLevelEquipTutorialStep()
    {
        if (PlayerAccountInfo.Me.tutorialProgress != "Started" &&
            PlayerAccountInfo.Me.tutorialProgress != "GotchaFinished")
        {
            return 7;
        }
        
        var valid = nineSlot.ValidateWarn();
        if (PlayerAccountInfo.Me.tutorialProgress == "GotchaFinished" && 
            valid != SkillSet.SkillEditError.Perfect)
        {
            return 6;
        }
        
        if (valid == SkillSet.SkillEditError.Perfect)
        {
            return 5;
        }

        // Filled/blocked first-column sets need repair rather than a drag
        // instruction with no available destination. Auto Fill can rebuild them.
        var empty = nineSlot.GetEmptySlots();
        if (empty.Count == 0 || (!HasTargetSpLevelStone(new[] { 0 })
            && !empty.Any(slot => (slot.num - 1) % 3 == 0))) return 4;
        
        if (!HasTargetSpLevelStone(new [] {0}))
        {
            return 0;
        }
        if (!HasTargetSpLevelStone(new [] {0, 1}))
        {
            return 1;
        }
        if (!HasTargetSpLevelStone(new [] {0, 1, 2}))
        {
            return 2;
        }
        if (!HasTargetSpLevelStone(new [] {0, 1, 2, 3}))
        {
            return 3;
        }
        return 4;
    }
    
    public void ExtraTipForSpStoneEquip()
    {
        int step = FullSpLevelEquipTutorialStep();
        if (step == 7)
        {
            ClearTutorialGuidance();
            return;
        }

        stonesBox.TutorialSimpleMode();
        nineSlot.ForceFirstColumn(step == 0);
        // Select/render the inventory before activating its drag demonstration.
        // The old order pointed at whichever empty slot existed on the previous page.
        if (step < 4)
        {
            if (step != equippedGuideStep || stonesBox.SelectedSpLevel != step)
            {
                stonesBox.PressTab(step);
                stonesBox.ScrollRect.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
            }
            var guide = new[] { spStoneGuide1, spStoneGuide2, spStoneGuide3, spStoneGuide4 }[step];
            foreach (var pointer in guide.GetComponentsInChildren<SkillEditTutorial11>(true))
                pointer.SetTargets(stonesBox.GetVisibleStoneCell(step)?.transform as RectTransform,
                    nineSlot.GetTutorialPlacementSlot(step)?._cell.transform as RectTransform);
        }
        equippedGuideStep = step;
        spStoneGuide1.SetActive(step == 0);
        spStoneGuide2.SetActive(step == 1);
        spStoneGuide3.SetActive(step == 2);
        spStoneGuide4.SetActive(step == 3);
        spStoneGuide5.SetActive(step == 4);
        spStoneGuide6.SetActive(step == 5);
        
        clickAutoEditIndicator.SetActive(step == 4 || step == 6);
        clickAutoEditIndicator2.SetActive(step == 6);
        nineSlot.confirmBtnIndicator.SetActive(step == 5);
        // Labels and moving hands describe the real controls; they must never
        // become drop/raycast targets over the skill stones or confirm button.
        foreach (var guide in TutorialGuides())
            foreach (var graphic in guide.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    IEnumerable<GameObject> TutorialGuides()
    {
        return new[] { spStoneGuide1, spStoneGuide2, spStoneGuide3, spStoneGuide4,
            spStoneGuide5, spStoneGuide6, clickAutoEditIndicator, clickAutoEditIndicator2 };
    }

    public void ClearTutorialGuidance()
    {
        foreach (var guide in TutorialGuides()) guide.SetActive(false);
        nineSlot.confirmBtnIndicator.SetActive(false);
        nineSlot.ForceFirstColumn(false);
        stonesBox.SetTutorialSimpleMode(false);
        equippedGuideStep = -1;
    }
}

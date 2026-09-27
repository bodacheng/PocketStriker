using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public partial class SkillEditLayer : UILayer
{
    [Header("Tutorial")] 
    [SerializeField] GameObject spStoneGuide1, spStoneGuide2, spStoneGuide3, spStoneGuide4, spStoneGuide5, spStoneGuide6;
    [SerializeField] ClickNextTutorial clickNextTutorial1, clickNextTutorial2;
    [SerializeField] GameObject clickAutoEditIndicator, clickAutoEditIndicator2;
    [SerializeField] GameObject mask;
    
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
        if (PlayerAccountInfo.Me.tutorialProgress == "SkillEditFinished2" ||
            PlayerAccountInfo.Me.tutorialProgress == "Finished")
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
        nineSlot.ForceFirstColumn(step == 0);
        spStoneGuide1.SetActive(step == 0);
        spStoneGuide2.SetActive(step == 1);
        spStoneGuide3.SetActive(step == 2);
        spStoneGuide4.SetActive(step == 3);
        spStoneGuide5.SetActive(step == 4);
        spStoneGuide6.SetActive(step == 5);
        
        clickAutoEditIndicator.SetActive(step == 6);
        clickAutoEditIndicator2.SetActive(step == 6);
        
        switch (step)
        {
            case 0:
                stonesBox.PressTab(0);
                break;
            case 1:
                stonesBox.PressTab(1);
                break;
            case 2:
                stonesBox.PressTab(2);
                break;
            case 3:
                stonesBox.PressTab(3);
                break;
            case 4:
                OpenAutoEditIndicator();
                break;
            case 5:
                CloseAutoEditIndicator();
                break;
        }

        if (step != 7)
        {
            stonesBox.TutorialSimpleMode();
        }
    }
}

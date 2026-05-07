using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;

public partial class SSLevelUpManager
{
    partial void OnLevelUpAllStonesRequested()
    {
        PreScene.target.trySwitchToStep(MainSceneStep.StoneUpdateConfirm);
    }

    UniTask ExecuteLevelUpStone(string instanceId, List<string> mInstanceIds, Action<string> refreshStoneData)
    {
        StoneLevelUpProccessor.LevelUpStone(instanceId, mInstanceIds, refreshStoneData);
        return UniTask.CompletedTask;
    }

    partial void OnCloseLevelUpPageTargetSelected()
    {
        _stoneListLayer.SkillStoneDetail.SkillIntro.gameObject.SetActive(true);
    }
}

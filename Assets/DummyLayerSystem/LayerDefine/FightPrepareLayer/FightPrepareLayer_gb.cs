using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using mainMenu;

public partial class FightPrepareLayer : UILayer
{
    #region Gangbang
    [SerializeField] GangbangHeroIcon gangbangFighterIcon;
    [SerializeField] private Text team1Flg;
    [SerializeField] private Text team2Flg;
    [SerializeField] private Text team1WholeCount;
    [SerializeField] private Text team2WholeCount;
    private Func<int, string, int, bool, int> _setTeamUnitCount;
    private Func<int, string, int> _getTeamUnitCount;
    #endregion
    
    public void SetGangbangFeature(
        Action toGangbangFront, string gangbangStageNo, 
        Func<int, string ,int, bool, int> setTeamUnitCount, Func<int, string, int> getTeamUnitCount)
    {
        fightModeSwitch.gameObject.SetActive(false);
        
        arcadeStageNoText.gameObject.SetActive(true);
        arcadeStageNoText.text = "Stage " + gangbangStageNo;
        toArcadeFrontBtn.gameObject.SetActive(PlayerAccountInfo.Me.tutorialProgress == "Finished");
        toArcadeFrontBtn.SetListener(toGangbangFront);
        
        var rewardDic = PlayFabReadClient.GangbangAwards;
        var reward = rewardDic[gangbangStageNo];
        rewardUI.ShowRewards(reward.d,reward.g);
        int.TryParse(gangbangStageNo, out var arcadeStageNoInt);
        rewardUI.AwardRender(PlayerAccountInfo.Me.gangbangProcess + 1 > arcadeStageNoInt);
        rewardUI.gameObject.SetActive(true);
        nineForShow.AddOnClickToSlots(
            (RECORD_ID) =>
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
            }
        );
        _setTeamUnitCount = (i, s, arg3 ,f) =>
        {
            var returnValue = setTeamUnitCount(i, s, arg3,f);
            if (i == 1)
            {
                team1Flg.text = Translate.Get("Player") + Translate.Get("WholeUnitCount");
                team1WholeCount.text = returnValue + "/" + CommonSetting.GangbangModeMaxUnitPerTeam;
            }
            else
            {
                team2Flg.text = Translate.Get("Enemy") + Translate.Get("WholeUnitCount");
                team2WholeCount.text = returnValue + "/" + CommonSetting.GangbangModeMaxUnitPerTeam;
            }
            return setTeamUnitCount(i,s,arg3,f);
        };
        _getTeamUnitCount = getTeamUnitCount;
    }
    
    public void GangbangStageMembersInfoShow(GangbangInfo stage)
    {
        GangbangInfosShow(stage.FightMembers.HeroSets.GetValues(), (x) =>
        {
            PreScene.target.Focusing.id = x;
            PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit);
        }, myTeamShowT, true, 1, PlayerAccountInfo.Me.tutorialProgress == "Finished");
        
        if (stage.FightMembers.HeroSets.GetValues().Count < 1)
        {
            teamEditIndicatorText.text = Translate.Get("HasExtraSeat");
            teamEditIndicator.SetActive(true);
        }
        else
        {
            teamEditIndicatorText.text = string.Empty; // Translate.Get("MakeYourTeam");
            teamEditIndicator.SetActive(false);
        }
        var icons = GangbangInfosShow(stage.FightMembers.EnemySets.GetValues(), 
            (x) =>
            {
                FocusTeam2Unit(x, stage.FightMembers.EnemySets.GetValues());
            },
            enemyTeamShowT, false, 2);
        icons.FirstOrDefault()?.iconButton.onClick.Invoke();
        team1Name.text = "YOU";
    }
    
    List<GangbangHeroIcon> GangbangInfosShow(List<UnitInfo> unitSets, Action<string> iconBehaviour, RectTransform showT, 
        bool withSkillCheck, int team, bool btnInteractive = true)
    {
        foreach (Transform t in showT)
        {
            Destroy(t.gameObject);
        }
        var icons = new List<GangbangHeroIcon>();
        int wholeTeamCount = 0;
        foreach(var unitInfo in unitSets)
        {
            wholeTeamCount += _getTeamUnitCount(team, unitInfo.id);
        }
        if (wholeTeamCount > CommonSetting.GangbangModeMaxUnitPerTeam)
        {
            foreach (var unitInfo in unitSets)
            {
                _setTeamUnitCount(team, unitInfo.id, CommonSetting.GangbangModeMaxUnitPerTeam / unitSets.Count, true);
            }
        }
        
        wholeTeamCount = 0;
        foreach(var unitInfo in unitSets)
        {
            var v = GangbangHeroIcon.ArrangeGangbangHeroIconToParent(
                (x) => _setTeamUnitCount(team, unitInfo.id, x, false),
                ()=> _getTeamUnitCount(team, unitInfo.id),
                gangbangFighterIcon, unitInfo, iconBehaviour,
                showT, withSkillCheck, team == 1, true, unitIconSize);
            v.iconButton.interactable = btnInteractive;
            icons.Add(v);
            wholeTeamCount += _getTeamUnitCount(team, unitInfo.id);
        }
        
        if (team == 1)
        {
            team1Flg.text = Translate.Get("Player") + Translate.Get("WholeUnitCount");
            team1WholeCount.text = wholeTeamCount + "/" + CommonSetting.GangbangModeMaxUnitPerTeam;
        }
        else
        {
            team2Flg.text = Translate.Get("Enemy") + Translate.Get("WholeUnitCount");
            team2WholeCount.text = wholeTeamCount + "/" + CommonSetting.GangbangModeMaxUnitPerTeam;
        }
        return icons;
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using mainMenu;

public partial class FightPrepareLayer : UILayer
{
    #region Gangbang
    [SerializeField] GangbangHeroIcon gangbangFighterIcon;
    [SerializeField] Text team1Flg;
    [SerializeField] Text team2Flg;
    [SerializeField] Text team1WholeCount;
    [SerializeField] Text team2WholeCount;

    [SerializeField] Text groupCount1;
    [SerializeField] Text groupCount2;
    [SerializeField] Text groupCount3;

    [SerializeField] BOButton countSet1;
    [SerializeField] BOButton countSet2;
    [SerializeField] BOButton countSet3;

    [SerializeField] GameObject countSelectedFrame1;
    [SerializeField] GameObject countSelectedFrame2;
    [SerializeField] GameObject countSelectedFrame3;

    private GangbangInfo _gangbangStage;
    private Func<int, string, int, int, int> _setTeamUnitCount;
    private Func<int, string, int> _getTeamUnitCount;
    private List<GangbangHeroIcon> _gangbangHeroIconsM;
    private List<GangbangHeroIcon> _gangbangHeroIconsE;
    #endregion

    private int _selectedMaxTeamCount;

    public int SelectedMaxTeamCount
    {
        get => _selectedMaxTeamCount;
        set => _selectedMaxTeamCount = value;
    }

    public void SetGangbangFeature(
        GangbangInfo stage, Action toGangbangFront, string gangbangStageNo,
        Func<int, string ,int, int, int> setTeamUnitCount, Func<int, string, int> getTeamUnitCount)
    {
        _gangbangStage = stage;
        SelectedMaxTeamCount = GetConfiguredMaxTeamCount();

        SetupCountOptionButton(
            countSet1, countSelectedFrame1, 1,
            CommonSetting.GangbangModeMaxUnitPerTeam1,
            countSelectedFrame2, countSelectedFrame3);
        SetupCountOptionButton(
            countSet2, countSelectedFrame2, 2,
            CommonSetting.GangbangModeMaxUnitPerTeam2,
            countSelectedFrame1, countSelectedFrame3);
        SetupCountOptionButton(
            countSet3, countSelectedFrame3, 3,
            CommonSetting.GangbangModeMaxUnitPerTeam3,
            countSelectedFrame1, countSelectedFrame2);

        SetText(groupCount1, CommonSetting.GangbangModeMaxUnitPerTeam1.ToString());
        SetText(groupCount2, CommonSetting.GangbangModeMaxUnitPerTeam2.ToString());
        SetText(groupCount3, CommonSetting.GangbangModeMaxUnitPerTeam3.ToString());

        if (fightModeSwitch != null)
        {
            fightModeSwitch.gameObject.SetActive(false);
        }

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
        if (nineForShowE != null && connectorE != null)
        {
            nineForShowE.AddOnClickToSlots(
                (RECORD_ID) =>
                {
                    var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                    connectorE.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
                }
            );
        }

        _setTeamUnitCount = (i, s, arg3, maxCount) =>
        {
            var returnValue = setTeamUnitCount(i, s, arg3, maxCount);
            RefreshCountDisplay(i, returnValue, maxCount);
            return returnValue;
        };
        _getTeamUnitCount = getTeamUnitCount;
    }

    public async UniTask GangbangStageUnitsDisplay(GangbangInfo stage, CancellationToken token)
    {
        var heroUnits = stage.FightMembers.HeroSets.GetValues();
        var enemyUnits = stage.FightMembers.EnemySets.GetValues();
        var heroLookup = BuildUnitLookup(heroUnits);
        var enemyLookup = BuildUnitLookup(enemyUnits);

        WarmupUnitIcons(heroUnits);
        WarmupUnitIcons(enemyUnits);

        var enemyConnector = connectorE != null ? connectorE : connector;
        var enemyNineForShow = nineForShowE != null ? nineForShowE : nineForShow;

        UniTask FocusHero(string instanceId) =>
            FocusTeamUnit(instanceId, heroLookup, connector, nineForShow);

        UniTask FocusEnemy(string instanceId) =>
            FocusTeamUnit(instanceId, enemyLookup, enemyConnector, enemyNineForShow);

        var focusTasks = new List<UniTask>();

        _gangbangHeroIconsM = GangbangInfosShow(
            heroUnits,
            id => FocusHero(id).Forget(),
            myTeamShowT,
            true,
            1,
            PlayerAccountInfo.Me.tutorialProgress == "Finished");

        var defaultHeroId = _gangbangHeroIconsM.FirstOrDefault()?.InstanceID;
        WarmupUnitModels(heroUnits, defaultHeroId, connector, _preparedHeroModelIds, token);

        if (heroUnits.Count < 1)
        {
            teamEditIndicatorText.text = Translate.Get("HasExtraSeat");
            teamEditIndicator.SetActive(true);
        }
        else
        {
            teamEditIndicatorText.text = string.Empty;
            teamEditIndicator.SetActive(false);
        }

        _gangbangHeroIconsE = GangbangInfosShow(
            enemyUnits,
            id => FocusEnemy(id).Forget(),
            enemyTeamShowT,
            false,
            2);

        var defaultEnemyId = _gangbangHeroIconsE.FirstOrDefault()?.InstanceID;
        WarmupUnitModels(enemyUnits, defaultEnemyId, enemyConnector, _preparedEnemyModelIds, token);

        ApplySelectedCountOption();

        if (!string.IsNullOrEmpty(defaultHeroId))
        {
            focusTasks.Add(FocusHero(defaultHeroId));
        }

        if (!string.IsNullOrEmpty(defaultEnemyId))
        {
            focusTasks.Add(FocusEnemy(defaultEnemyId));
        }

        await UniTask.WhenAll(focusTasks);

        if (token.IsCancellationRequested)
        {
            return;
        }

        _gangbangHeroIconsE.FirstOrDefault()?.iconButton.onClick.Invoke();
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
            var v = GangbangHeroIcon.ArrangeGangbangHeroIconToParent(
                (x) => _setTeamUnitCount(team, unitInfo.id, x, SelectedMaxTeamCount),
                () => _getTeamUnitCount(team, unitInfo.id),
                gangbangFighterIcon, unitInfo, iconBehaviour,
                showT, withSkillCheck, team == 1, true, unitIconSize);
            v.iconButton.interactable = btnInteractive;
            icons.Add(v);
            wholeTeamCount += _getTeamUnitCount(team, unitInfo.id);
        }
        RefreshCountDisplay(team, wholeTeamCount, SelectedMaxTeamCount);
        return icons;
    }

    void SetupCountOptionButton(
        BOButton button, GameObject selectedFrame, int option, int maxCount, params GameObject[] otherFrames)
    {
        if (button == null)
        {
            return;
        }

        button.SetListener(() =>
        {
            PlayerPrefs.SetInt("gangbangCountOption", option);
            PlayerPrefs.Save();
            ApplyUnitCountSetting(maxCount);
            selectedFrame?.SetActive(true);
            foreach (var frame in otherFrames)
            {
                frame?.SetActive(false);
            }
        });
    }

    int GetConfiguredMaxTeamCount()
    {
        switch (PlayerPrefs.GetInt("gangbangCountOption", 1))
        {
            case 2:
                return CommonSetting.GangbangModeMaxUnitPerTeam2;
            case 3:
                return CommonSetting.GangbangModeMaxUnitPerTeam3;
            default:
                return CommonSetting.GangbangModeMaxUnitPerTeam1;
        }
    }

    void ApplySelectedCountOption()
    {
        switch (PlayerPrefs.GetInt("gangbangCountOption", 1))
        {
            case 2:
                if (countSet2 != null)
                {
                    countSet2.onClick.Invoke();
                    return;
                }
                break;
            case 3:
                if (countSet3 != null)
                {
                    countSet3.onClick.Invoke();
                    return;
                }
                break;
            default:
                if (countSet1 != null)
                {
                    countSet1.onClick.Invoke();
                    return;
                }
                break;
        }

        ApplyUnitCountSetting(GetConfiguredMaxTeamCount());
    }

    void ApplyUnitCountSetting(int maxUnitPerTeam)
    {
        if (_gangbangStage == null)
        {
            return;
        }

        SelectedMaxTeamCount = Mathf.Max(1, maxUnitPerTeam);
        var team1UnitCount = _gangbangStage.GangbangAutoAdjustTeamUnitByMaxCount(
            1, _gangbangStage.FightMembers.HeroSets.GetValues(), SelectedMaxTeamCount, true);
        var team2UnitCount = _gangbangStage.GangbangAutoAdjustTeamUnitByMaxCount(
            2, _gangbangStage.FightMembers.EnemySets.GetValues(), SelectedMaxTeamCount, true);

        RefreshCountDisplay(1, team1UnitCount, SelectedMaxTeamCount);
        RefreshCountDisplay(2, team2UnitCount, SelectedMaxTeamCount);
    }

    void RefreshCountDisplay(int teamID, int currentTeamUnitCount, int maxTeamCount)
    {
        if (teamID == 1)
        {
            SetText(team1Flg, Translate.Get("Player") + Translate.Get("WholeUnitCount"));
            SetText(team1WholeCount, currentTeamUnitCount + "/" + maxTeamCount);
            RefreshIconCounts(_gangbangHeroIconsM);
        }
        else
        {
            SetText(team2Flg, Translate.Get("Enemy") + Translate.Get("WholeUnitCount"));
            SetText(team2WholeCount, currentTeamUnitCount + "/" + maxTeamCount);
            RefreshIconCounts(_gangbangHeroIconsE);
        }
    }

    static void RefreshIconCounts(List<GangbangHeroIcon> icons)
    {
        if (icons == null)
        {
            return;
        }

        foreach (var icon in icons)
        {
            icon?.RefreshCount();
        }
    }

    static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}

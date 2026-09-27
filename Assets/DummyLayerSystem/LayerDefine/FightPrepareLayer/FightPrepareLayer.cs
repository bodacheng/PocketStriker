using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using mainMenu;
using ModelView;
using Singleton;

public partial class FightPrepareLayer : UILayer
{
    [SerializeField] HeroIcon fighterIcon;
    [SerializeField] RectTransform myTeamShowT;
    [SerializeField] RectTransform enemyTeamShowT;
    [SerializeField] float unitIconSize = 200;
    [SerializeField] BOButton editTeamButton; // 根据进入战斗模式决定是否显示
    [SerializeField] GameObject teamEditIndicator;
    [SerializeField] Text teamEditIndicatorText;
    [SerializeField] GameObject modeFlgR;
    [SerializeField] GameObject modeFlgM;
    [SerializeField] GameObject modeFlgE;
    [SerializeField] GameObject modeFlgG;
    [SerializeField] FightModeSwitch fightModeSwitch;
    [SerializeField] GameObject enemyDoubleExModeFlg;
    [SerializeField] GameObject enemyInfiniteExModeFlg;
    [SerializeField] FightBeginBtn beginFight;
    [SerializeField] Text team1Name;
    [SerializeField] Text team2Name;
    [SerializeField] Text team1OneWord;
    [SerializeField] Text team2OneWord;
    [SerializeField] Text arcadeStageNoText;
    [SerializeField] RewardUI rewardUI;
    [SerializeField] BOButton toArcadeFrontBtn;
    [SerializeField] Image view2D;
    [SerializeField] Animator layerAnimator;
    [SerializeField] Animator unitOutAnimator;
    [SerializeField] NineForShow nineForShow;
    [SerializeField] NineForShow nineForShowE;
    [SerializeField] DedicatedCameraConnector connector;
    [SerializeField] DedicatedCameraConnector connectorE;
    [Header("战场选择")]
    [SerializeField] BattleGroundSwitch battleGroundSwitch;

    public BattleGroundSwitch BattleGroundSwitch => battleGroundSwitch;

    readonly HashSet<string> _preparedHeroModelIds = new HashSet<string>(StringComparer.Ordinal);
    readonly HashSet<string> _preparedEnemyModelIds = new HashSet<string>(StringComparer.Ordinal);
    readonly HashSet<string> _warmedIconRecordIds = new HashSet<string>(StringComparer.Ordinal);

    int _displayVersion;
    readonly SingleThreadProcessor _previewQueue = new SingleThreadProcessor();
    readonly Dictionary<NineForShow, int> _previewVersions = new Dictionary<NineForShow, int>();

    bool IsActiveDisplay(int version, CancellationToken token) =>
        this != null && !IsClosing && version == _displayVersion && !token.IsCancellationRequested;

    static void ClearUnitIcons(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    public override void OnDestroy()
    {
        _displayVersion++;
        _previewVersions.Clear();
        ClearUnitIcons(myTeamShowT);
        ClearUnitIcons(enemyTeamShowT);
        base.OnDestroy();
    }

    public void SetLayerAnimatorTrigger(string code)
    {
        layerAnimator.SetTrigger(code);
    }

    public void SetFightMode(int fightMode)
    {
        fightModeSwitch.Setup(fightMode, PlayerPrefs.GetInt("preferAdventureMode",  PlayerPrefs.GetInt("preferAdventureMode", 2)));
    }

    public TeamMode GetSetFightMode()
    {
        return fightModeSwitch.TeamMode;
    }

    public void SetFightBeginFeature(Action fightBegin)
    {
        beginFight.SetAction(fightBegin);
    }

    public void SetFightBeginEnableRender(bool canFight, bool guide = false)
    {
        beginFight.Enable(canFight, guide);
    }


    public void SetTeamEditFeature(Action teamEdit)
    {
        editTeamButton.onClick.RemoveAllListeners();
        editTeamButton.onClick.AddListener(()=> teamEdit());
    }

    public void SetArcadeFeature(Action toArcadeFront, string arcadeStageNo)
    {
        arcadeStageNoText.gameObject.SetActive(true);
        arcadeStageNoText.text = "Stage " + arcadeStageNo;
        toArcadeFrontBtn.gameObject.SetActive(PlayerAccountInfo.Me.tutorialProgress == "Finished");
        toArcadeFrontBtn.SetListener(toArcadeFront);

        var rewardDic = PlayFabReadClient.StageAwards;
        var reward = rewardDic[arcadeStageNo];
        rewardUI.ShowRewards(reward.d,reward.g);
        int.TryParse(arcadeStageNo, out var arcadeStageNoInt);
        rewardUI.AwardRender(PlayerAccountInfo.Me.arcadeProcess + 1 > arcadeStageNoInt);
        rewardUI.gameObject.SetActive(true);
        nineForShow.AddOnClickToSlots(
            (RECORD_ID) =>
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
            }
        );
    }

    public void SetEventFeature(string arcadeStageNo)
    {
        arcadeStageNoText.gameObject.SetActive(true);
        var hardTxt = string.Empty;
        if (arcadeStageNo.Contains("easy"))
        {
            hardTxt = "easy";
        }
        if (arcadeStageNo.Contains("normal"))
        {
            hardTxt = "normal";
        }
        if (arcadeStageNo.Contains("hard"))
        {
            hardTxt = "hard";
        }
        arcadeStageNoText.text = Translate.Get("EventFightTitle")+ " " + hardTxt;
        toArcadeFrontBtn.gameObject.SetActive(false);
        rewardUI.gameObject.SetActive(false);
        // rewardUI.ShowRewards(award.d,award.g);
        // rewardUI.AwardRender(cleared);
        nineForShow.AddOnClickToSlots(
            (RECORD_ID) =>
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
            }
        );
    }

    public async UniTask StageMembersInfoShow(FightInfo stage, CancellationToken token)
    {
        var version = ++_displayVersion;
        var heroUnits = stage.FightMembers.HeroSets.GetValues();
        var enemyUnits = stage.FightMembers.EnemySets.GetValues();
        var enemyLookup = BuildUnitLookup(enemyUnits);
        WarmupUnitIcons(heroUnits);
        WarmupUnitIcons(enemyUnits);
        MemberInfosShow(heroUnits,
            (x) =>
            {
                if (!IsActiveDisplay(version, token)) return;
                PreScene.target.Focusing.id = x;
                PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit);
            },
            myTeamShowT, true, PlayerAccountInfo.Me.tutorialProgress == "Finished");

        bool hasExtraSeat = (stage.EventType != FightEventType.Quest &&
                             (dataAccess.Units.Dic.Count > stage.FightMembers.HeroSets.GetValues().Count
                              && stage.FightMembers.HeroSets.GetValues().Count < 3));

        if (hasExtraSeat)
        {
            teamEditIndicatorText.text = Translate.Get("HasExtraSeat");
            teamEditIndicator.SetActive(true);
        }
        else //if (dataAccess.Units.Dic.Count > 0 && stage.FightMembers.HeroSets.GetValues().Count == 0)
        {
            teamEditIndicatorText.text = string.Empty; // Translate.Get("MakeYourTeam");
            teamEditIndicator.SetActive(false);
        }

        MemberInfosShow(enemyUnits,
            id => FocusTeamUnit(id, enemyLookup, connector, nineForShow, version, token).Forget(),
            enemyTeamShowT, false);
        var defaultEnemyId = enemyUnits.FirstOrDefault()?.id;
        await FocusTeamUnit(defaultEnemyId, enemyLookup, connector, nineForShow, version, token);
        if (!IsActiveDisplay(version, token)) return;
        WarmupUnitModels(enemyUnits, defaultEnemyId, connector, _preparedEnemyModelIds, token);

        if (stage.EventType == FightEventType.Arena)
        {
            team1Name.text = stage.Team1LeaderboardEntry?.DisplayName;
            team2Name.text = stage.Team2LeaderboardEntry?.DisplayName;
            team1OneWord.text = stage.Team1OneWord;
            team2OneWord.text = stage.Team2OneWord;
        }
        else
        {
            team1Name.text = "YOU";
        }

        enemyDoubleExModeFlg.SetActive(stage.team2CGMode == CriticalGaugeMode.DoubleGain);
        enemyInfiniteExModeFlg.SetActive(stage.team2CGMode == CriticalGaugeMode.Unlimited);
    }

    async UniTask FocusTeamUnit(string instanceId, IReadOnlyDictionary<string, UnitInfo> teamUnits,
        DedicatedCameraConnector targetConnector, NineForShow targetNineForShow, int displayVersion, CancellationToken token)
    {
        if (!IsActiveDisplay(displayVersion, token) || string.IsNullOrEmpty(instanceId) ||
            teamUnits == null || !teamUnits.TryGetValue(instanceId, out var info)) return;

        if (targetConnector == null) targetConnector = connector;
        if (targetNineForShow == null) targetNineForShow = nineForShow;
        if (targetConnector == null || targetNineForShow == null) return;

        _previewVersions.TryGetValue(targetNineForShow, out var previousVersion);
        var previewVersion = previousVersion + 1;
        _previewVersions[targetNineForShow] = previewVersion;
        bool IsCurrent() => IsActiveDisplay(displayVersion, token) && targetConnector != null &&
            targetNineForShow != null && _previewVersions.TryGetValue(targetNineForShow, out var current) &&
            current == previewVersion;

        try
        {
            // The model/skill renderers do not accept cancellation. Serialize their writes and
            // skip superseded queued requests so a slower old selection cannot overwrite the latest.
            await _previewQueue.RunAsQueued(async () =>
            {
                if (!IsCurrent()) return;
                ProgressLayer.Loading(string.Empty);
                try
                {
                    await UniTask.WhenAll(
                        targetNineForShow.SkillSetInfoOfUnitOnArcadePage(info.set),
                        targetConnector.ShowModel(info.r_id));
                }
                catch (Exception) when (!IsCurrent()) { }
                finally
                {
                    if (IsCurrent()) ProgressLayer.Close();
                }
            }).AttachExternalCancellation(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    List<HeroIcon> MemberInfosShow(List<UnitInfo> heroSets, Action<string> iconBehaviour, RectTransform _showT, bool withSkillCheck, bool btnInteractive = true)
    {
        ClearUnitIcons(_showT);
        var icons = new List<HeroIcon>();
        foreach(var oneMember in heroSets)
        {
            var v = HeroIcon.ArrangeHeroIconToParent(fighterIcon, oneMember, iconBehaviour, _showT, unitIconSize, withSkillCheck, true);
            v.iconButton.interactable = btnInteractive;
            icons.Add(v);
        }
        return icons;
    }

    static Dictionary<string, UnitInfo> BuildUnitLookup(List<UnitInfo> units)
    {
        var lookup = new Dictionary<string, UnitInfo>(units?.Count ?? 0, StringComparer.Ordinal);
        if (units == null)
        {
            return lookup;
        }

        foreach (var unit in units)
        {
            if (!string.IsNullOrEmpty(unit?.id))
            {
                lookup[unit.id] = unit;
            }
        }

        return lookup;
    }

    void WarmupUnitIcons(IEnumerable<UnitInfo> units)
    {
        if (units == null)
        {
            return;
        }

        foreach (var unit in units)
        {
            var recordId = unit?.r_id;
            if (string.IsNullOrEmpty(recordId) || !_warmedIconRecordIds.Add(recordId))
            {
                continue;
            }

            UniTask.Void(async () =>
            {
                try
                {
                    await UnitIconDic.Load(recordId);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FightPrepareLayer] Icon warmup failed for {recordId}: {ex.Message}");
                }
            });
        }
    }

    void WarmupUnitModels(List<UnitInfo> units, string skipInstanceId, DedicatedCameraConnector targetConnector, HashSet<string> preparedIds, CancellationToken token)
    {
        if (targetConnector == null || units == null || preparedIds == null)
        {
            return;
        }

        var version = _displayVersion;
        var preloadTasks = new List<UniTask>();
        foreach (var unit in units)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }
            if (unit == null || unit.id == skipInstanceId)
            {
                continue;
            }

            var recordId = unit.r_id;
            if (string.IsNullOrEmpty(recordId) || !preparedIds.Add(recordId))
            {
                continue;
            }

            preloadTasks.Add(_previewQueue.RunAsQueued(async () =>
            {
                if (!IsActiveDisplay(version, token) || targetConnector == null)
                {
                    preparedIds.Remove(recordId);
                    return;
                }
                try { await targetConnector.PrepareModel(recordId); }
                catch
                {
                    preparedIds.Remove(recordId);
                    throw;
                }
            }));
        }

        if (preloadTasks.Count == 0)
        {
            return;
        }

        UniTask.Void(async () =>
        {
            try
            {
                await UniTask.WhenAll(preloadTasks).AttachExternalCancellation(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FightPrepareLayer] Model warmup failed: {ex.Message}");
            }
        });
    }

    public void TutorialForceFightBegin()
    {
        editTeamButton.gameObject.SetActive(false);
    }
}

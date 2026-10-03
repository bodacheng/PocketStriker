using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using dataAccess;
using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public class QuestInfoPage : MSceneProcess
{
    private FightPrepareLayer _layer;
    private GangbangInfo _controllingGangbangInfo = null;

    int _enterVersion;
    CancellationTokenSource _enterCancellation;

    bool IsActiveEnter(int version, CancellationToken token) =>
        version == _enterVersion && !token.IsCancellationRequested && _layer != null && !_layer.IsClosing;

    async UniTask EnterProcess(FightInfo stage, int version, CancellationToken token)
    {
        if (stage == null) return;
        stage.ApplyBattleModeRules();
        try
        {
            var represent = stage.GetRepresentUnitInfo();
            UnitConfig unitConfig = Units.GetUnitConfig(represent.r_id);
            BackGroundPS.target.ChangeBGByElement(unitConfig.element);

            FightLoad.Fight = stage;
            _layer = FightLoad.Fight.IsGroupBattle
                ? UILayerLoader.Load<FightPrepareLayer>(false, "FightPrepareLayer_gb")
                : UILayerLoader.Load<FightPrepareLayer>();

            switch (FightLoad.Fight.EventType)
            {
                case FightEventType.Arena:
                    _layer.SetArenaFeature();
                    FightLoad.Fight.FightMembers.HeroSets = TeamSet.GetTargetSet("arena").LoadTeamDic();
                    void GoToTeamEditArena()
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "arena", true);
                    }

                    _layer.SetLayerAnimatorTrigger("normal");
                    await _layer.BattleGroundSwitch.INI().AttachExternalCancellation(token);
                    if (!IsActiveEnter(version, token)) return;
                    _layer.BattleGroundSwitch.gameObject.SetActive(true);
                    _layer.SetTeamEditFeature(GoToTeamEditArena);

                    break;
                case FightEventType.Quest:
                    if (stage.IsGroupBattle)
                    {
                        ConfigureGroupPreparation((GangbangInfo)stage);
                        break;
                    }
                    _layer.SetLayerAnimatorTrigger(FightLoad.Fight.EvolutionMode ? "evolution" : "normal");

                    // Tutorial and evolution fights use one starter; mixed battles
                    // use the three-slot team, preserving both saved lineups.
                    var adventureTeamKey = AdventureModeRules.GetTeamSetKey(
                        FightLoad.Fight.ID, FightLoad.Fight.EvolutionMode);
                    if (AdventureModeRules.UsesSingleHero(FightLoad.Fight.ID, FightLoad.Fight.EvolutionMode))
                    {
                        var arcadeTeam = TeamSet.GetTargetSet(adventureTeamKey);
                        foreach (var position in arcadeTeam.PosNumsWithLocalKeys)
                        {
                            if (position.posNum != 0)
                            {
                                arcadeTeam.SetPosUnitByInstanceID(position.posNum, null);
                            }
                        }
                    }

                    FightLoad.Fight.LoadMyTeam();
                    void GoToTeamEditArcade()
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, adventureTeamKey, true);
                    }
                    _layer.SetTeamEditFeature(GoToTeamEditArcade);
                    _layer.SetArcadeFeature(
                        () =>
                        {
                            PreScene.target.trySwitchToStep(MainSceneStep.ArcadeFront, false);
                        },
                        FightLoad.Fight.ID,
                        FightLoad.Fight.ArcadeFightMode
                    );
                    break;
                case FightEventType.Event:
                    void GoToOriginTeamEditArcade()
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "origin", true);
                    }
                    _layer.SetLayerAnimatorTrigger("normal");
                    FightLoad.Fight.FightMembers.HeroSets = TeamSet.GetTargetSet("origin").LoadTeamDic();
                    _layer.SetTeamEditFeature(GoToOriginTeamEditArcade);
                    _layer.SetEventFeature(FightLoad.Fight.ID);
                    break;
                case FightEventType.Gangbang:
                    ConfigureGroupPreparation((GangbangInfo)stage);
                    break;
            }

            if (stage.IsGroupBattle)
            {
                await _layer.GangbangStageUnitsDisplay(_controllingGangbangInfo, token);
            }
            else
            {
                await _layer.StageMembersInfoShow(stage, token);
            }

            if (!IsActiveEnter(version, token)) return;
            if (FightLoad.Fight.IsGroupBattle)
            {
                _layer.SetFightMode(1);
                _layer.SetFightBeginFeature(()=> GoToFight(_controllingGangbangInfo, _layer.SelectedMaxTeamCount));
            }
            else
            {
                _layer.SetFightMode(BattleModeRules.GetPreparationMode(FightLoad.Fight.FightMode));
                _layer.SetFightBeginFeature(()=> GoToFight(FightLoad.Fight));
            }

            var canFight = CanFightCheck(FightLoad.Fight, _controllingGangbangInfo);
            //_layer.TeamEditIndicator.gameObject.SetActive(!canFight);
            _layer.SetFightBeginEnableRender(canFight, PlayerAccountInfo.Me.tutorialProgress != "Finished");
            SetLoaded(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    void ConfigureGroupPreparation(GangbangInfo stage)
    {
        _layer.SetLayerAnimatorTrigger("normal");
        var controlling = GangbangInfo.Copy(stage);
        _controllingGangbangInfo = controlling;
        FightLoad.Fight = controlling;
        controlling.LoadMyTeam();
        _layer.SetTeamEditFeature(() =>
            PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "gangbang", true));
        _layer.SetGangbangFeature(controlling,
            () => PreScene.target.trySwitchToStep(controlling.EventType == FightEventType.Quest
                ? MainSceneStep.ArcadeFront : MainSceneStep.GangBangFront, false), controlling.ID,
            (team, id, count, maxCount) =>
            {
                var whole = controlling.SetTeamUnitCount(team, id, count, maxCount);
                if (team == 1)
                {
                    PlayerPrefs.SetInt("gangbangPos" + id, controlling.GetTeamUnitCount(team, id));
                    PlayerPrefs.Save();
                }
                _layer.SetFightBeginEnableRender(CanFightCheck(controlling, controlling));
                return whole;
            },
            (team, id) => controlling.GetTeamUnitCount(team, id, team == 1));
    }

    void BeginEnter(FightInfo stage)
    {
        CancelEnter();
        SetLoaded(false);
        _controllingGangbangInfo = null;
        _enterCancellation = new CancellationTokenSource();
        EnterProcess(stage, _enterVersion, _enterCancellation.Token).Forget();
    }

    void CancelEnter()
    {
        _enterVersion++;
        _enterCancellation?.Cancel();
        _enterCancellation?.Dispose();
        _enterCancellation = null;
    }

    public QuestInfoPage()
    {
        Step = MainSceneStep.QuestInfo;
    }

    public override void ProcessEnter()
    {
        BeginEnter(FightLoad.Fight);
    }

    public override void ProcessEnter<T>(T t)
    {
        if (t is GangbangInfo)
        {
            BeginEnter(t as GangbangInfo);
        }
        else
        {
            BeginEnter(t as FightInfo);
        }
    }

    public override void ProcessEnd()
    {
        CancelEnter();
        SetLoaded(false);
        _controllingGangbangInfo = null;
        UILayerLoader.Remove<FightPrepareLayer>();
        _layer = null;
    }

    bool CanFightCheck(FightInfo fight, GangbangInfo refGangbangInfo = null)
    {
        if (fight.IsGroupBattle)
        {
            var group = refGangbangInfo ?? fight as GangbangInfo;
            return group != null && group.GetGroupWholeUnitCount(1) > 0 && group.GetGroupWholeUnitCount(2) > 0
                && group.FightMembers.CheckStonesLegal(fight.EventType, group.GetNonZeroInstanceIds(1));
        }
        switch (fight.EventType)
        {
            case FightEventType.Arena:
                if (fight.FightMembers.HeroSets.GetValues().Count != 3)
                {
                    return false;
                }
                break;
            case FightEventType.Quest:
                var adventureTeamCount = fight.FightMembers.HeroSets.GetValues().Count;
                if (!AdventureModeRules.IsValidHeroCount(fight.ID, fight.EvolutionMode, adventureTeamCount))
                {
                    return false;
                }
                break;
            case FightEventType.Event:
                if (PlayerAccountInfo.Me.tutorialProgress == "SkillEditFinished2")
                {
                    if (fight.FightMembers.HeroSets.GetValues().Count < 2)
                    {
                        return false;
                    }
                }
                else
                {
                    if (fight.FightMembers.HeroSets.GetValues().Count == 0)
                    {
                        return false;
                    }
                }
                break;
            default:
                if (fight.FightMembers.HeroSets.GetValues().Count == 0)
                {
                    return false;
                }
                break;
        }

        if (!fight.FightMembers.CheckStonesLegal(fight.EventType)) return false;

        return true;
    }

    void GoToFight(FightInfo fightInfo, int maxTeamUnitCount = -1)
    {
        if (fightInfo.IsGroupBattle)
        {
            GoToGroupFight((GangbangInfo)fightInfo, maxTeamUnitCount);
            return;
        }
        // if (!fightInfo.FightMembers.CheckStonesLegal(fightInfo.EventType))
        // {
        //     PopupLayer.ArrangeWarnWindow(Translate.Get("TeamUnitNotFull"));
        //     return;
        // }

        fightInfo.ApplyBattleModeRules();

        switch (fightInfo.EventType)
        {
            case FightEventType.Arena:
                if (fightInfo.FightMembers.HeroSets.GetValues().Count != 3)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("TeamNotFull"));
                    return;
                }
                CloudScript.SubtractVirtualCurrency(
                    "TK",1,
                    () =>
                    {
                        fightInfo.LoadMyTeam();
                        fightInfo.battleGroundID = _layer.BattleGroundSwitch.BattleFieldIndex;
                        FightLoad.Go(fightInfo);
                    }
                );
                break;
            case FightEventType.Quest:
                fightInfo.LoadMyTeam();
                if (!CanFightCheck(fightInfo) || fightInfo.FightMembers.EnemySets.GetValues().Count == 0)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("TeamNotFull"));
                    return;
                }
                var adventureTeamCount = fightInfo.FightMembers.HeroSets.GetValues().Count;
                if (!AdventureModeRules.UsesSingleHero(fightInfo.ID, fightInfo.EvolutionMode) &&
                    adventureTeamCount < 3 &&
                    dataAccess.Units.Dic.Count > adventureTeamCount)
                {
                    PopupLayer.ArrangeConfirmWindow(
                        () => { FightLoad.Go(fightInfo);},
                        Translate.Get("HasExtraSeatButFight"));
                    return;
                }
                FightLoad.Go(fightInfo);
                break;
            default:
                fightInfo.LoadMyTeam();
                if (fightInfo.FightMembers.HeroSets.GetValues().Count < 1 || fightInfo.FightMembers.EnemySets.GetValues().Count < 1)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("TeamNotFull"));
                    return;
                }
                if (dataAccess.Units.Dic.Count >= 3 && fightInfo.FightMembers.HeroSets.GetValues().Count < 3)
                {
                    PopupLayer.ArrangeConfirmWindow(
                        () => { FightLoad.Go(fightInfo);},
                        Translate.Get("HasExtraSeatButFight"));
                    return;
                }

                FightLoad.Go(fightInfo);
                break;
        }
    }

    void GoToGroupFight(GangbangInfo group, int maxTeamUnitCount)
    {
        if (!CanFightCheck(group, group))
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("TeamNotFull"));
            return;
        }
        group.team1Mode = group.team2Mode = TeamMode.MultiRaid;
        if (maxTeamUnitCount <= 0) maxTeamUnitCount = GangbangInfo.GetConfiguredTeamLimit();
        void Begin()
        {
            group.RecordTeamLimit(maxTeamUnitCount);
            group.ConvertTeamToGangbang();
            FightScene.FightScene.team1GroupSet = GangbangInfo.CopyGroupSets(group.Team1GroupSet);
            group.Team1ID = PlayerAccountInfo.Me.PlayFabId;
            FightLoad.Go(group);
        }
        if (group.GetGroupWholeUnitCount(1) < maxTeamUnitCount)
        {
            PopupLayer.ArrangeConfirmWindow(Begin, Translate.Get("HasExtraSeatForGangbangButFight"));
            return;
        }
        Begin();
    }
}

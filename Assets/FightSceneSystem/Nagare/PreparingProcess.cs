using System;
using FightScene;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UniRx;
using UnityEngine;

public class PreparingProcess : FSceneProcess
{
    private FightingStepLayer fightingStepLayer;
    
    public PreparingProcess()
    {
        Step = SceneStep.Preparing;
        nextProcessStep = SceneStep.CountDown;
    }
    
    async UniTask EnterProcess()
    {
        RTFightManager.Target.team1.Clear();
        RTFightManager.Target.team2.Clear();
        BoundaryControlByGod.target.ConfigureBattleRadius(false);
        
        RTFightManager.Target._CameraManager.VisibilityControl.Clear();
        
        if ((FightLoad.Fight.EventType == FightEventType.Quest || FightLoad.Fight.EventType == FightEventType.Gangbang || FightLoad.Fight.EventType == FightEventType.Event))
        {
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
            FightScene.FightScene.target.LoadAds();
#endif
        }
        
        RTFightManager.Target.Disposables?.Dispose();
        RTFightManager.Target.Disposables = new CompositeDisposable();
        
        Sensor.ClearFightingMember();
        UILayerLoader.Remove<ArenaFightOver>();
        RTFightManager.Target._CameraManager.Assign_Camera(C_Mode.NULL, null,null);
        RTFightManager.Target._CameraManager.SetPosToStart();
        UILayerLoader.Load<ProgressLayer>(true, null, true);
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), 0.5f);
        
        var heroUnits = FightLoad.Fight.FightMembers.HeroSets.GetValues();
        var enemyUnits = FightLoad.Fight.FightMembers.EnemySets.GetValues();
        var effectPreloadCount = FightLoad.Fight.team1Mode == TeamMode.Rotation ? 1 :
            Mathf.Max(1, Mathf.Max(heroUnits.Count, enemyUnits.Count));

        // Pool loaders consult the label index synchronously. Finish indexing before
        // starting them, otherwise the first battle can skip valid effect resources.
        await AddressablesLogic.Essentials();
        var tasks = new List<UniTask>
        {
            AppSetting.PlayBGM(FightLoad.Fight.GetBGMKey()),
            HurtObjectManager.ConstructDPool(),
            BoundaryControlByGod.target.ChangeBackGround(FightLoad.Fight.battleGroundID),
            EffectsManager.IniEffectsPool(CommonSetting.HitGroundEffectCode, null, effectPreloadCount),
            EffectsManager.IniEffectsPool(CommonSetting.WallCrackEffectCode, null, effectPreloadCount)
        };

        var elementCounts = new Dictionary<Element, int>();
        void CountElements(List<UnitInfo> units)
        {
            foreach (var unit in units)
            {
                var config = Units.GetUnitConfig(unit.r_id);
                if (config == null)
                    continue;
                elementCounts.TryGetValue(config.element, out var count);
                elementCounts[config.element] = count + 1;
            }
        }
        CountElements(heroUnits);
        CountElements(enemyUnits);

        foreach (var element in elementCounts)
        {
            var effectPath = FightGlobalSetting.EffectPathDefine(element.Key);
            tasks.Add(EffectsManager.IniEffectsPool("light_hit", effectPath, element.Value));
            tasks.Add(EffectsManager.IniEffectsPool("heavy_hit", effectPath, element.Value));
            tasks.Add(EffectsManager.IniEffectsPool("super_hit", effectPath, element.Value));
            tasks.Add(EffectsManager.IniEffectsPool("electric_s_e", effectPath, element.Value));
        }

        // Shared effects need one pool each, regardless of the teams' element count.
        var sharedEffectCount = Mathf.Max(effectPreloadCount, heroUnits.Count + enemyUnits.Count);
        tasks.Add(EffectsManager.IniEffectsPool("super_combo_explosion", null, sharedEffectCount));
        tasks.Add(EffectsManager.IniEffectsPool("dream_buff", null, sharedEffectCount));
        if (FightLoad.Fight.team1Mode == TeamMode.Rotation && FightLoad.Fight.team2Mode == TeamMode.Rotation)
        {
            tasks.Add(EffectsManager.IniEffectsPool(CommonSetting.MemberShiftEffectCode, null, 1));
        }

        var completedPreloads = 0;
        var unitProgress = 0f;
        var loadingBattleText = Translate.Get("LoadingBattle");
        void ReportProgress()
        {
            var resourceProgress = completedPreloads / (float)tasks.Count;
            ProgressLayer.LoadingPercent(loadingBattleText,
                Mathf.Lerp(0.5f, 0.75f, unitProgress * 0.8f + resourceProgress * 0.2f), false);
        }
        var unitLoading = RTFightManager.Target.LoadUnits(FightLoad.Fight, progress =>
        {
            unitProgress = Mathf.Max(unitProgress, progress);
            ReportProgress();
        });
        async UniTask TrackPreload(UniTask task)
        {
            await task;
            completedPreloads++;
            ReportProgress();
        }
        var trackedTasks = new List<UniTask>(tasks.Count);
        foreach (var task in tasks)
            trackedTasks.Add(TrackPreload(task));
        trackedTasks.Add(unitLoading);
        await UniTask.WhenAll(trackedTasks);

        var teamMembers = new Dictionary<TeamConfig, List<Data_Center>>();
        RTFightManager.Target.heroTeamConfig.playID = FightLoad.Fight.Team1ID;
        RTFightManager.Target.EnemyTeamConfig.playID = FightLoad.Fight.Team2ID;
        
        DicAdd<TeamConfig, List<Data_Center>>.Add(teamMembers, RTFightManager.Target.heroTeamConfig, RTFightManager.Target.team1.teamMembers.GetValues());
        DicAdd<TeamConfig, List<Data_Center>>.Add(teamMembers, RTFightManager.Target.EnemyTeamConfig, RTFightManager.Target.team2.teamMembers.GetValues());
        FightLogger.value.ReadyToLog(teamMembers);
        
        RTFightManager.Target.team1.TeamMode = FightLoad.Fight.team1Mode;
        RTFightManager.Target.team2.TeamMode = FightLoad.Fight.team2Mode;
        RTFightManager.Target.team1.teamConfig = RTFightManager.Target.heroTeamConfig;
        RTFightManager.Target.team2.teamConfig = RTFightManager.Target.EnemyTeamConfig;
        RTFightManager.Target.team1.Auto = FightLoad.Fight.Team1Auto;
        RTFightManager.Target.team2.Auto = FightLoad.Fight.RunTutorial ? false : FightLoad.Fight.Team2Auto;
        
        if (FightLoad.Fight.EventType == FightEventType.Screensaver)
        {
            RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
            RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
        }else{
            RTFightManager.Target.team1.TurnAllUnitsInvincible(FightGlobalSetting._Team1Invincible);
            RTFightManager.Target.team2.TurnAllUnitsInvincible(false);
        }
        
        switch (RTFightManager.Target.team1.TeamMode)
        {
            case TeamMode.MultiRaid:
                RTFightManager.Target.team1.InitializeMulti(
                    FightLoad.Fight.team1HpRate, FightLoad.Fight.team1CGMode, 
                    FightLoad.Fight.team1AIMode, FightLoad.Fight.dumbAIDecisionDelay,
                    CreateRandomBoolFunc(
                        FightLoad.Fight.IsGroupBattle ? 100: FightGlobalSetting._player1DreamComboAIRateNumM)
                );
                break;
            case TeamMode.Rotation:
                RTFightManager.Target.team1.TeamsIniRotate(
                    FightLoad.Fight.team1HpRate, FightLoad.Fight.team1CGMode, 
                    FightLoad.Fight.team1AIMode, FightLoad.Fight.dumbAIDecisionDelay,
                    CreateRandomBoolFunc(0)
                );
                break;
        }
        
        switch (RTFightManager.Target.team2.TeamMode)
        {
            case TeamMode.MultiRaid:
                RTFightManager.Target.team2.InitializeMulti(
                    FightLoad.Fight.team2HpRate, FightLoad.Fight.team2CGMode, 
                    FightLoad.Fight.team2AIMode, FightLoad.Fight.dumbAIDecisionDelay,
                    CreateRandomBoolFunc(
                        FightLoad.Fight.IsGroupBattle ? 100 :
                            (FightLoad.Fight.EventType == FightEventType.Arena ? 
                                FightGlobalSetting.ArenaEnemyDreamComboAIRate: FightLoad.Fight.dreamComboAIRateNum))
                );
                break;
            case TeamMode.Rotation:
                RTFightManager.Target.team2.TeamsIniRotate(
                    FightLoad.Fight.team2HpRate, FightLoad.Fight.team2CGMode, 
                    FightLoad.Fight.team2AIMode, FightLoad.Fight.dumbAIDecisionDelay,
                    CreateRandomBoolFunc(FightLoad.Fight.EventType == FightEventType.Arena ? 
                        FightGlobalSetting.ArenaEnemyDreamComboAIRate: FightLoad.Fight.dreamComboAIRateNum)
                );
                break;
        }
        
        if (FightLoad.Fight.RunTutorial)
            RTFightManager.Target.team2.TutorialSpecial();
        
        RTFightManager.Target.SetGame(FightLoad.Fight);
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattleAboutToEnd"), 0.8f);
        fightingStepLayer = FightingStepLayer.Open();
        await fightingStepLayer.Setup(false);
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattleAboutToEnd"), 1f);
        switch (RTFightManager.Target.team1.TeamMode)
        {
            case TeamMode.MultiRaid:
                RTFightManager.Target.team1.ToStartPosMulti();
                break;
            case TeamMode.Rotation:
                RTFightManager.Target.team1.ToStartPosRotate();
                break;
        }
        
        switch (RTFightManager.Target.team2.TeamMode)
        {
            case TeamMode.MultiRaid:
                RTFightManager.Target.team2.ToStartPosMulti();
                break;
            case TeamMode.Rotation:
                RTFightManager.Target.team2.ToStartPosRotate();
                break;
        }

        RTFightManager.Target.FacePreparedTeamsTowardEachOther();
        var formationBounds = new List<Bounds>();
        if (FightLoad.Fight.IsGroupBattle)
        {
            void AddFormationBounds(UnitsManger team)
            {
                foreach (var unit in team.teamMembers.mDict.Values)
                    if (unit != null && BattleCameraFraming.TryGetModelBounds(unit.WholeT, out var bounds))
                        formationBounds.Add(bounds);
            }
            AddFormationBounds(RTFightManager.Target.team1);
            AddFormationBounds(RTFightManager.Target.team2);
        }
        BoundaryControlByGod.target.ConfigureBattleRadius(FightLoad.Fight.IsGroupBattle,
            BattleFormationPlacement.RequiredArenaRadius(formationBounds, 1f));
        fightingStepLayer.SetSensor();
        
        RTFightManager.Target.team1.RMode_Unit.Subscribe(x =>
            {
                RTFightManager.Target.CameraAdjustment(RTFightManager.playerTeam, RTFightManager.Target.team1.TeamMode, FightLoad.Fight.EventType);
            }
        ).AddTo(RTFightManager.Target.Disposables);
        
        RTFightManager.Target.team2.RMode_Unit.Subscribe(x =>
            {
                RTFightManager.Target.CameraAdjustment(RTFightManager.playerTeam, RTFightManager.Target.team1.TeamMode, FightLoad.Fight.EventType);
            }
        ).AddTo(RTFightManager.Target.Disposables);
        
        ProgressLayer.Close();
    }
    
    public override void ProcessEnter()
    {
        // Also starts a fresh optional request for an in-scene retry/next battle.
        FightScene.FightScene.target.PreloadAIStory();
        //HighLightLayer.DarkOff(Color.white, 0, true);
        // Background covers the notch/home-indicator area; UILayer keeps text safe.
        // Place it above battle UI, then EnterProcess puts the progress bar on top.
        var unitInstructionLayer = UILayerLoader.Load<UnitInstructionLayer>(true, null, true);
        unitInstructionLayer.LoadUnitImage();
        EnterProcess().Forget();
    }
    
    public override void ProcessEnd()
    {
        FightScene.FightScene.target.LoadStageFinished.Value = false;
        //HighLightLayer.LightUp(1f);
        UILayerLoader.Remove<UnitInstructionLayer>();
    }
    
    public override bool CanEnterOtherProcess()
    {
        return FightScene.FightScene.target.LoadStageFinished.Value
               && RTFightManager.Target.team1.IfAllUnitsPreparedForBattle()
               && RTFightManager.Target.team2.IfAllUnitsPreparedForBattle()
               && (fightingStepLayer != null && fightingStepLayer.Initialized);
    }
    
    Func<bool> CreateRandomBoolFunc(int probabilityPercentage)
    {
        probabilityPercentage = Mathf.Clamp(probabilityPercentage, 0, 100);
        return () => UnityEngine.Random.Range(0, 100) < probabilityPercentage;
    }
}

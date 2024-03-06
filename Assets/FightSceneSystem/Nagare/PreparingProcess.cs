using System;
using FightScene;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UniRx;

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
        
        if ((FightLoad.Fight.EventType == FightEventType.Quest || FightLoad.Fight.EventType == FightEventType.Gangbang)
            &&
            !PlayerAccountInfo.Me.noAdsState)
        {
            FightScene.FightScene.target.LoadAds();
        }

        RTFightManager.Target.Disposables = new CompositeDisposable();
        
        Sensor.ClearFightingMember();
        UILayerLoader.Remove<ArenaFightOver>();
        RTFightManager.Target._CameraManager.Assign_Camera(C_Mode.NULL, null,null);
        RTFightManager.Target._CameraManager.SetPosToStart();
        UILayerLoader.Load<ProgressLayer>();
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), 0.5f);
        
        var tasks = new List<UniTask>
        {
            AppSetting.PlayBGM(FightLoad.Fight.GetBGMKey()),
            HurtObjectManager.ConstructDPool(),
            AddressablesLogic.Essentials(),
            BoundaryControlByGod.target.ChangeBackGround(FightLoad.Fight.battleGroundID),
            RTFightManager.Target.LoadUnits(FightLoad.Fight),
            EffectsManager.IniEffectsPool(CommonSetting.HitGroundEffectCode, null, 3),
            EffectsManager.IniEffectsPool(CommonSetting.WallCrackEffectCode, null, 3),
            EffectsManager.IniEffectsPool(CommonSetting.BreakFreeEffectCode, null, 3),
            EffectsManager.IniEffectsPool(CommonSetting.MemberShiftEffectCode, null, 3)
        };
        ProgressLayer.LoadingPercent(Translate.Get("LoadingBattle"), 0.7f);
        await UniTask.WhenAll(tasks);
        
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
                        FightLoad.Fight.EventType == FightEventType.Gangbang ? 100: FightGlobalSetting._player1DreamComboAIRateNumM)
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
                        FightLoad.Fight.EventType == FightEventType.Gangbang ? 100 : 
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
        fightingStepLayer = UILayerLoader.Load<FightingStepLayer>();
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
        //HighLightLayer.DarkOff(Color.white, 0, true);
        var unitInstructionLayer = UILayerLoader.Load<UnitInstructionLayer>();
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
        // 使用Random类生成随机数
        Random random = new Random();

        // 生成一个介于0到99之间的随机数
        int randomNumber = random.Next(100);

        // 如果随机数小于等于给定的概率百分比，返回true；否则返回false
        return () => randomNumber <= probabilityPercentage;
    }
}

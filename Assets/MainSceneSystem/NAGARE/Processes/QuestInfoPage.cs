using Cysharp.Threading.Tasks;
using dataAccess;
using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public class QuestInfoPage : MSceneProcess
{
    private FightPrepareLayer _layer;
    private GangbangInfo _controllingGangbangInfo = null;
    
    async UniTask EnterProcess(FightInfo stage)
    {
        FightLoad.Fight = stage;
        _layer = UILayerLoader.Load<FightPrepareLayer>();
        
        switch (FightLoad.Fight.EventType)
        {
            case FightEventType.Arena:
                FightLoad.Fight.FightMembers.HeroSets = TeamSet.GetTargetSet("arena").LoadTeamDic();
                void GoToTeamEditArena()
                {
                    PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "arena", true);
                }
                
                await _layer.BattleGroundSwitch.INI();
                _layer.BattleGroundSwitch.gameObject.SetActive(true);
                _layer.SetTeamEditFeature(GoToTeamEditArena);
                break;
            case FightEventType.Quest:
                FightLoad.Fight.FightMembers.HeroSets = TeamSet.GetTargetSet("arcade").LoadTeamDic();
                void GoToTeamEditArcade()
                {
                    PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "arcade", true);
                }
                _layer.SetTeamEditFeature(GoToTeamEditArcade);
                _layer.SetArcadeFeature(
                    () =>
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.ArcadeFront, false);
                    },
                    FightLoad.Fight.ID
                );
                break;
            case FightEventType.Gangbang:
                _controllingGangbangInfo = GangbangInfo.Copy((GangbangInfo)stage);
                _controllingGangbangInfo.FightMembers.HeroSets = TeamSet.GetTargetSet("gangbang").LoadTeamDic(); // 为了队员显示
                _layer.SetTeamEditFeature(
                    () =>
                    {
                        PreScene.target.trySwitchToStep(MainSceneStep.TeamEditFront, "gangbang", true);
                    }
                );
                
                _layer.SetGangbangFeature(
                    () => { PreScene.target.trySwitchToStep(MainSceneStep.GangBangFront, false); },
                    FightLoad.Fight.ID,
                    (x, y ,z, f)=>
                    {
                        var whole = _controllingGangbangInfo.SetTeamUnitCount(x, y, z, f);
                        if (x == 1) // 本地存储各个gangbang人数
                        {
                            PlayerPrefs.SetInt("gangbangPos"+ y, _controllingGangbangInfo.GetTeamUnitCount(x,y));
                            PlayerPrefs.Save();
                        }
                        
                        var canFight = CanFightCheck(FightLoad.Fight, _controllingGangbangInfo);
                        //_layer.TeamEditIndicator.gameObject.SetActive(!canFight);
                        _layer.SetFightBeginEnableRender(canFight);
                        return whole;
                    },
                    (x,y)=> _controllingGangbangInfo.GetTeamUnitCount(x,y,x == 1));
                break;
        }
        
        if (stage is GangbangInfo)
        {
            _layer.GangbangStageMembersInfoShow(_controllingGangbangInfo);
        }
        else
        {
            _layer.StageMembersInfoShow(stage);
        }
        
        if (FightLoad.Fight.EventType == FightEventType.Gangbang)
        {
            _layer.SetFightMode(1);
            _layer.SetFightBeginFeature(()=> GoToFight(_controllingGangbangInfo));
        }
        else
        {
            int FightMode()
            {
                switch (FightLoad.Fight.EventType)
                {
                    case FightEventType.Quest:
                        return FightLoad.Fight.ArcadeFightMode;
                    default:
                        return 0;
                }
            }
            _layer.SetFightMode(FightMode());
            _layer.SetFightBeginFeature(()=> GoToFight(FightLoad.Fight));
        }
        
        var canFight = CanFightCheck(FightLoad.Fight, _controllingGangbangInfo);
        //_layer.TeamEditIndicator.gameObject.SetActive(!canFight);
        _layer.SetFightBeginEnableRender(canFight);
        SetLoaded(true);
    }
    
    public QuestInfoPage()
    {
        Step = MainSceneStep.QuestInfo;
    }
    
    public override void ProcessEnter()
    {
        EnterProcess(FightLoad.Fight).Forget();
    }
    
    public override void ProcessEnter<T>(T t)
    {
        if (t is GangbangInfo)
        {
            EnterProcess(t as GangbangInfo).Forget();
        }
        else
        {
            EnterProcess(t as FightInfo).Forget();
        }
    }
    
    public override void ProcessEnd()
    {
        _controllingGangbangInfo = null;
        UILayerLoader.Remove<FightPrepareLayer>();
    }
    
    bool CanFightCheck(FightInfo fight, GangbangInfo refGangbangInfo = null)
    {
        switch (fight.EventType)
        {
            case FightEventType.Arena:
                if (fight.FightMembers.HeroSets.GetValues().Count != 3)
                {
                    return false;
                }
                break;
            case FightEventType.Quest:
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
            case FightEventType.Gangbang:
                var unitCountFit = refGangbangInfo.GetGroupWholeUnitCount(1) > 0
                                   && refGangbangInfo.GetGroupWholeUnitCount(2) > 0;
                if (!unitCountFit)
                    return false;
                break;
            default:
                if (fight.FightMembers.HeroSets.GetValues().Count == 0)
                {
                    return false;
                }
                break;
        }

        if (fight.EventType == FightEventType.Gangbang)
        {
            var instanceIds = refGangbangInfo.GetNonZeroInstanceIds(1);
            if (!fight.FightMembers.CheckStonesLegal(fight.EventType, instanceIds))
            {
                return false;
            }
        }
        else
        {
            if (!fight.FightMembers.CheckStonesLegal(fight.EventType))
            {
                return false;
            }
        }
        
        return true;
    }
    
    void GoToFight(FightInfo fightInfo)
    {
        // if (!fightInfo.FightMembers.CheckStonesLegal(fightInfo.EventType))
        // {
        //     PopupLayer.ArrangeWarnWindow(Translate.Get("TeamUnitNotFull"));
        //     return;
        // }
        
        fightInfo.team1Mode = _layer.GetSetFightMode();
        fightInfo.team2Mode = _layer.GetSetFightMode();
        
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
            case FightEventType.Gangbang:
                if (fightInfo.FightMembers.HeroSets.GetValues().Count < 1 || fightInfo.FightMembers.EnemySets.GetValues().Count < 1)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("TeamNotFull"));
                    return;
                }
                
                var bangBangInfo = ((GangbangInfo)fightInfo);

                void RealFight()
                {
                    bangBangInfo.ConvertTeamToGangbang();
                    FightScene.FightScene.team1GroupSet = bangBangInfo.Team1GroupSet;
                    fightInfo.Team1ID = PlayerAccountInfo.Me.PlayFabId;
                    FightLoad.Go(fightInfo);
                }
                
                if (bangBangInfo.GetGroupWholeUnitCount(1) < CommonSetting.GangbangModeMaxUnitPerTeam)
                {
                    PopupLayer.ArrangeConfirmWindow(
                        RealFight,
                        Translate.Get("HasExtraSeatForGangbangButFight"));
                    return;
                }

                RealFight();
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
}

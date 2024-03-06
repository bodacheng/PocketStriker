using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using PlayFab.ClientModels;
using UnityEngine;

namespace FightScene
{
    public class FightOverProcess : FSceneProcess
    {
        public FightOverProcess()
        {
            Step = SceneStep.FightOver;
        }
        
        void EnterProcess()
        {
            // 竞技场结束：显示排名变化？
            // quest结束：显示技能石经验获得情况和报酬信息？
            // 自我战斗结束：显示战斗分析？
            // 技能测试：显示战斗分析？
            
            switch (FightLoad.Fight.EventType)
            {
                case FightEventType.Arena:
                    if (FightLogger.value.GetWinnerId() == PlayerAccountInfo.Me.PlayFabId)
                    {
                        CloudScript.ArenaPointUp(
                            FightLoad.Fight.Team1LeaderboardEntry,
                            FightLoad.Fight.Team2LeaderboardEntry,
                            (x,y, z) =>
                            {
                                var a = UILayerLoader.Load<ArenaFightOver>();
                                a.Setup();
                                a.Step2Anim();
                                a.ShowArenaPoint(x,y);
                                a.ShowAward(z,0, 0);
                            }
                        );
                    }
                    else
                    {
                        var a = UILayerLoader.Load<ArenaFightOver>();
                        a.Setup();
                        a.Step2Anim();
                    }
                    //FightOverControl.target.ShowSKillSets(RealTimeGameProcessManager.target.FightTeam1);
                break;
                case FightEventType.Quest:
                    if (FightLogger.value.GetWinnerId() == PlayerAccountInfo.Me.PlayFabId)
                    {
                        var levelInt = Convert.ToInt32(FightLoad.Fight.ID);
                        if (levelInt > PlayerAccountInfo.Me.arcadeProcess)
                        {
                            CloudScript.ArcadeProgress(
                                FightLoad.Fight.ID,
                                result =>
                                {
                                    var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                                    var hasReward = jsonResult.ContainsKey("has_reward") ? jsonResult["has_reward"] : false;
                                    var hasRewardBool = (bool)hasReward;
                                    var arenaFightOver = UILayerLoader.Load<ArenaFightOver>();
                                    arenaFightOver.Setup();
                                    arenaFightOver.Step2Anim();
                                    if (hasRewardBool)
                                    {
                                        var rewardGd = jsonResult.ContainsKey("gold") ? jsonResult["gold"] : 0;
                                        var rewardDm = jsonResult.ContainsKey("diamond") ? jsonResult["diamond"] : 0;
                                        PlayerAccountInfo.Me.arcadeProcess = levelInt;
                                        var rewardGdInt = Convert.ToInt32(rewardGd);
                                        var rewardDmInt = Convert.ToInt32(rewardDm);
                                        arenaFightOver.ShowAward(rewardDmInt, rewardGdInt, 
                                            levelInt % 5 == 0 ? PlayFabSetting._adBossFightRewardDM : PlayFabSetting._adNormalFightRewardDM,
                                            levelInt);
                                    }
                                    arenaFightOver.LoadNextArcadeStage();
                                    
                                    if (FightLoad.Fight.ID == "1")
                                    {
                                        PlayerAccountInfo.Me.tutorialProgress = "StageOneFinished";
                                        PlayFabReadClient.UpdateUserData(
                                            new UpdateUserDataRequest()
                                            {
                                                Data = new Dictionary<string, string>()
                                                {
                                                    { "TutorialProgress", "StageOneFinished" }
                                                }
                                            },
                                            () => {}
                                        );
                                    }
                                }
                            );
                        }
                        else
                        {
                            var a = UILayerLoader.Load<ArenaFightOver>();
                            a.Setup();
                            a.Step2Anim();
                        }
                    }
                    else
                    {
                        var a = UILayerLoader.Load<ArenaFightOver>();
                        a.Setup();
                        a.Step2Anim();
                        a.AgainBtn.gameObject.SetActive(true);
                    }
                    break;
                case FightEventType.Gangbang:
                    if (FightLogger.value.GetWinnerId() == PlayerAccountInfo.Me.PlayFabId)
                    {
                        var levelInt = Convert.ToInt32(FightLoad.Fight.ID);
                        if (levelInt > PlayerAccountInfo.Me.gangbangProcess)
                        {
                            CloudScript.GangbangProgress(
                                FightLoad.Fight.ID,
                                result =>
                                {
                                    var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                                    var hasReward = jsonResult.ContainsKey("has_reward") ? jsonResult["has_reward"] : false;
                                    var hasRewardBool = (bool)hasReward;
                                    var arenaFightOver = UILayerLoader.Load<ArenaFightOver>();
                                    arenaFightOver.Setup();
                                    arenaFightOver.Step2Anim();
                                    if (hasRewardBool)
                                    {
                                        var rewardGd = jsonResult.ContainsKey("gold") ? jsonResult["gold"] : 0;
                                        var rewardDm = jsonResult.ContainsKey("diamond") ? jsonResult["diamond"] : 0;
                                        PlayerAccountInfo.Me.gangbangProcess = levelInt;
                                        var rewardGdInt = Convert.ToInt32(rewardGd);
                                        var rewardDmInt = Convert.ToInt32(rewardDm);
                                        arenaFightOver.ShowAward(
                                            rewardDmInt, rewardGdInt, 
                                            levelInt % 5 == 0 ? PlayFabSetting._adBossFightRewardDM : PlayFabSetting._adNormalFightRewardDM,
                                            levelInt);
                                    }
                                    Int32.TryParse(FightLoad.Fight.ID, out var nowStageNo);
                                    var nextStageNo = nowStageNo + 1;
                                    arenaFightOver.LoadNextGangbangStage(nextStageNo);
                                }
                            );
                        }
                        else
                        {
                            var a = UILayerLoader.Load<ArenaFightOver>();
                            a.Setup();
                            a.Step2Anim();
                        }
                    }
                    else
                    {
                        var a = UILayerLoader.Load<ArenaFightOver>();
                        a.Setup();
                        a.Step2Anim();
                        a.AgainBtn.gameObject.SetActive(true);
                    }
                    break;
                case FightEventType.Self:
                    var c = UILayerLoader.Load<CommonFightResult>();
                    c.Setup(
                        ()=>FightScene.target.ReturnToFront(), 
                        () =>
                        {
                            LocalGameRestart(0);
                            UILayerLoader.Remove<CommonFightResult>();
                        },
                        () =>
                        {
                            LocalGameRestart(2);
                            UILayerLoader.Remove<CommonFightResult>();
                        },
                        () =>
                        {
                            LocalGameRestart(1);
                            UILayerLoader.Remove<CommonFightResult>();
                        }
                    );
                    //c.ShowSKillSets(FightingStepLayer.target.team1UI);
                    break;
                case FightEventType.SkillTest:
                    SkillTestReload();
                    break;
            }
            
            SingleAssignmentDisposableCleaner.Clear();
        }
        
        public override void ProcessEnter()
        {
            EnterProcess();
        }
        
        public override void ProcessEnd()
        {
            
        }
        
        void LocalGameRestart(int mode)
        {
            switch (mode)
            {
                case 0:
                    break;
                case 1:
                    FightLoad.Fight.team1Mode = TeamMode.MultiRaid;
                    FightLoad.Fight.team2Mode = TeamMode.MultiRaid;
                    break;
                case 2:
                    FightLoad.Fight.team1Mode = TeamMode.Rotation;
                    FightLoad.Fight.team2Mode = TeamMode.Rotation;
                    break;
            }
            FSceneProcessesRunner.Main.ChangeProcess(SceneStep.Preparing);
        }
        
        async void SkillTestReload()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3));
            FightLoad.Fight = FightInfo.RandomSkillTestStage(FightLoad.Fight.team1Mode);
            LocalGameRestart(0);
        }
    }
}
using System;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;

namespace FightScene
{
    public class FightResultAnim : FSceneProcess
    {
        private bool animEnd = false;
        
        public FightResultAnim()
        {
            Step = SceneStep.FightResultAnim;
            nextProcessStep = SceneStep.FightOver;
        }

        public override void ProcessEnter()
        {
            EnterProcess().Forget();
        }

        async UniTask EnterProcess()
        {
            await FinalMomentAnim();
        }

        public override bool CanEnterOtherProcess()
        {
            return animEnd;
        }

        async UniTask FinalMomentAnim()
        {
            animEnd = false;
            Time.timeScale = 0.4f;
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            var winners = new List<Data_Center>();
            switch (FightLogger.value.GetWinnerTeam())
            {
                case Team.player1:
                    winners = RTFightManager.Target.team1.teamMembers.GetValues();
                    break;
                case Team.player2:
                    winners = RTFightManager.Target.team2.teamMembers.GetValues();
                    break;
            }
            RTFightManager.Target._CameraManager.Assign_Camera(C_Mode.NULL, null,null);
            foreach (Data_Center one in winners)
            {
                if (!one.FightDataRef.IsDead.Value)
                {
                    one._MyBehaviorRunner.ChangeState("Victory");
                }
            }
            Time.timeScale = 1f;
            var arenaFightOver = UILayerLoader.Load<ArenaFightOver>();

            async UniTask EndPart()
            {
                arenaFightOver.Step1Anim();
                await UniTask.Delay(TimeSpan.FromSeconds(1));
                animEnd = true;
            }
            
            switch (FightLoad.Fight.EventType)
            {
                case FightEventType.Gangbang:
                case FightEventType.Quest:
                case FightEventType.Event:
                    await ShowStoryBeforeResultIfNeeded(arenaFightOver, EndPart);
                    break;
                default:
                    await EndPart();
                    break;
            }
        }

        private async UniTask ShowStoryBeforeResultIfNeeded(ArenaFightOver arenaFightOver, Func<UniTask> endPart)
        {
            if (!FightLogger.value.IsLocalPlayerWinner(RTFightManager.playerTeam, PlayerAccountInfo.Me.PlayFabId))
            {
                arenaFightOver.Setup();
                await endPart();
                return;
            }

            var fightScene = global::FightScene.FightScene.target;
            if (fightScene != null)
            {
                await fightScene.EnsureAIStory();
            }

            if (arenaFightOver.LoadStory())
            {
                arenaFightOver.Setup(async () =>
                {
                    await endPart();
                });
                return;
            }

            arenaFightOver.Setup();
            await endPart();
        }
    }
}

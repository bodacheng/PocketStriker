using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;

namespace FightScene
{
    public class CountDownProcess : FSceneProcess
    {
        const float CountDownFallbackSeconds = 8f;
        private FightingStepLayer fightingStepLayer;
        bool AutoMoveToNext;
        float enterRealtime;
        public CountDownProcess()
        {
            Step = SceneStep.CountDown;
            nextProcessStep = SceneStep.Fighting;
        }
        
        public override void ProcessEnter()
        {
            AutoMoveToNext = false;
            enterRealtime = Time.realtimeSinceStartup;
            //CameraMode nowC = RealTimeGameProcessManager.target._CameraManager.CModeDic[C_Mode.OneVOne];
            //if (nowC is OneVOneMode)
            //{
            //    ((OneVOneMode)nowC).xzMax = 100f;
            //}

            fightingStepLayer = FightingStepLayer.Open();
            fightingStepLayer.gameObject.SetActive(true);
            fightingStepLayer.PreparingMode(true);
            if (FightLoad.Fight.RunTutorial)
            {
                fightingStepLayer.Team1UI.AutoSwitch?.gameObject.SetActive(false);
            }
            BeforeFightCountDown().Forget();
        }
        
        async UniTask BeforeFightCountDown()
        {
            try
            {
                //RealTimeGameProcessManager.target.CameraParaAdjustment(RealTimeGameProcessManager.playerTeam);
                var cd = UILayerLoader.Load<CountDownLayer>();
                if (cd != null)
                {
                    await cd.BeforeFightCountDown();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                AutoMoveToNext = true;
            }
        }
        
        public override void ProcessEnd()
        {
            UILayerLoader.Remove<CountDownLayer>();
            if (FightLoad.Fight.RunTutorial)
            {
                fightingStepLayer.Team1UI.AutoSwitch?.gameObject.SetActive(true);
            }
        }
        
        public override bool CanEnterOtherProcess()
        {
            return AutoMoveToNext || Time.realtimeSinceStartup - enterRealtime >= CountDownFallbackSeconds;
        }
        
        public override void LocalUpdate()
        {
            if (fightingStepLayer != null)
            {
                RTFightManager.Target.team1.LocalUpdate();
                RTFightManager.Target.team2.LocalUpdate();
            }
            if (FightLoad.Fight.EventType != FightEventType.Gangbang && FightLoad.Fight.team1Mode != TeamMode.MultiRaid)
                RTFightManager.Target._CameraManager.VisibilityControl.LocalUpdate();
        }
    }
}

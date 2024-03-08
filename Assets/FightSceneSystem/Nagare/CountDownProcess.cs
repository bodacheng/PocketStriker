using Cysharp.Threading.Tasks;
using DummyLayerSystem;

namespace FightScene
{
    public class CountDownProcess : FSceneProcess
    {
        private FightingStepLayer fightingStepLayer;
        bool AutoMoveToNext;
        public CountDownProcess()
        {
            Step = SceneStep.CountDown;
            nextProcessStep = SceneStep.Fighting;
        }
        
        public override void ProcessEnter()
        {
            //CameraMode nowC = RealTimeGameProcessManager.target._CameraManager.CModeDic[C_Mode.OneVOne];
            //if (nowC is OneVOneMode)
            //{
            //    ((OneVOneMode)nowC).xzMax = 100f;
            //}
            
            fightingStepLayer = UILayerLoader.Load<FightingStepLayer>();
            fightingStepLayer.gameObject.SetActive(true);
            fightingStepLayer.PreparingMode(true);
            if (FightLoad.Fight.RunTutorial)
            {
                fightingStepLayer.Team1UI.AutoSwitch.gameObject.SetActive(false);
            }
            BeforeFightCountDown().Forget();
        }
        
        async UniTask BeforeFightCountDown()
        {
            AutoMoveToNext = false;
            //RealTimeGameProcessManager.target.CameraParaAdjustment(RealTimeGameProcessManager.playerTeam);
            var cd = UILayerLoader.Load<CountDownLayer>();
            await cd.BeforeFightCountDown();
            AutoMoveToNext = true;
        }
        
        public override void ProcessEnd()
        {
            UILayerLoader.Remove<CountDownLayer>();
            if (FightLoad.Fight.RunTutorial)
            {
                fightingStepLayer.Team1UI.AutoSwitch.gameObject.SetActive(true);
            }
        }
        
        public override bool CanEnterOtherProcess()
        {
            return AutoMoveToNext;
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
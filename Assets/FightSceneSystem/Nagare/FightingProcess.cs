using DummyLayerSystem;
using System.Collections.Generic;
using Log;

namespace FightScene
{
    public class FightingProcess : FSceneProcess
    {
        FightingStepLayer _layer;
        
        public FightingProcess()
        {
            Step = SceneStep.Fighting;
            nextProcessStep = SceneStep.FightOver;
        }
        
        public override bool CanEnterOtherProcess()
        {
            return FightLogger.value.GameOver.Value;
        }
        
        public override void ProcessEnter()
        {
            _layer = UILayerLoader.Get<FightingStepLayer>();
            if (FightLoad.Fight.EventType == FightEventType.Screensaver)
            {
                var titleScreenLayer = UILayerLoader.Load<TitleScreenLayer>();
                titleScreenLayer.Initialise();
                _layer.InputsManager.FocusUnit(null);
                HighLightLayer.LightUp(1f);
            }
            else
            {
                _layer.gameObject.SetActive(true);
                _layer.PreparingMode(false);
            }
            if (FightLoad.Fight.RunTutorial)
                _layer.OpenTutorial();
            RTFightManager.Target.ModeStart();
        }
        
        public override void ProcessEnd()
        {
            if (FightLoad.Fight.EventType == FightEventType.Screensaver)
            {
                UILayerLoader.Remove<TitleScreenLayer>();
            }
            else
            {
                FightingStepLayer.Close();
            }
            
            var dataCenters = new List<Data_Center>();
            dataCenters.AddRange(RTFightManager.Target.team1.teamMembers.GetValues());
            dataCenters.AddRange(RTFightManager.Target.team2.teamMembers.GetValues());
            HitBoxLogTable.Instance.SkillLog(dataCenters);
            RTFightManager.Target.Disposables.Dispose();
            RTFightManager.Target.RefreshTimeDic.Clear();
            RTFightManager.Target.ClearUnitData();
            FightLogger.value.WatchMissionsAbandon();
        }

        public override void LocalUpdate()
        {
            if (_layer != null)
            {
                RTFightManager.Target.team1.LocalUpdate();
                RTFightManager.Target.team2.LocalUpdate();
            }
            
            if (FightLoad.Fight.EventType != FightEventType.Gangbang && FightLoad.Fight.team1Mode != TeamMode.MultiRaid)
                RTFightManager.Target._CameraManager.VisibilityControl.LocalUpdate();
        }
    }
}
using DummyLayerSystem;

public class GoToStageOne : TutorialProcess
{
    private ReturnLayer _returnLayer;
    private FightPrepareLayer _fightPrepareLayer;
    private FightingStepLayer _fightingLayer;
    
    public override void ProcessEnter()
    {
    }
    
    public override void ProcessEnd()
    {
        HighLightLayer.Close();
    }
    
    public override bool CanEnterOtherProcess()
    {
        return _fightingLayer != null;
    }
    
    public override void LocalUpdate()
    {
        if (_returnLayer == null)
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
        
        if (_fightPrepareLayer == null)
        {
            _fightPrepareLayer = UILayerLoader.Get<FightPrepareLayer>();
            if (_fightPrepareLayer != null)
            {
                _fightPrepareLayer.TutorialForceFightBegin();
            }
        }

        if (_fightPrepareLayer != null && _returnLayer != null)
        {
            _returnLayer.gameObject.SetActive(false);
        }
        
        if (_fightingLayer == null)
        {
            _fightingLayer = UILayerLoader.Get<FightingStepLayer>();
        }
    }
}
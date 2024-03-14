using DummyLayerSystem;
using mainMenu;

public class GoTo : TutorialProcess
{
    private FrontLayer _frontLayer;
    private UpperInfoBar _upperInfoBar;
    private LowerMainBar _lowerMainBar;
    private ReturnLayer _returnLayer;

    private readonly MainSceneStep _goto;
    public GoTo(MainSceneStep step)
    {
        _goto = step;
    }
    
    public override void ProcessEnd()
    {
        _lowerMainBar?.CloseIndicators();
    }

    public override void LocalUpdate()
    {
        if (_lowerMainBar == null)
        {
            _lowerMainBar = UILayerLoader.Get<LowerMainBar>();
            if (_lowerMainBar != null)
            {
                _lowerMainBar.PlsClickBtn(_goto);
            }
        }
        
        if (_frontLayer == null)
        {
            _frontLayer = UILayerLoader.Get<FrontLayer>();
            if (_frontLayer != null)
            {
                _frontLayer.PlsClickBtn(_goto);
            }
        }
        
        if (_upperInfoBar == null)
        {
            _upperInfoBar = UILayerLoader.Get<UpperInfoBar>();
            if (_upperInfoBar != null)
            {
                _upperInfoBar.Interactable(false);
            }
        }
        
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
            if (_returnLayer != null)
                _returnLayer.gameObject.SetActive(false);
        }
    }
    
    public override bool CanEnterOtherProcess()
    {
        if (ProcessesRunner.Main.currentProcess == null)
        {
            return false;
        }
        return ProcessesRunner.Main.currentProcess.Step == _goto;
    }
}
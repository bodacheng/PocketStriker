using DummyLayerSystem;

public class ForceBack : TutorialProcess
{
    private readonly bool forceBack;
    private ReturnLayer _returnLayer;
    
    public delegate bool WaitOverDelegate();
    readonly WaitOverDelegate _waitForThis;
    
    public ForceBack(WaitOverDelegate waitOverDelegate, bool forBack = true)
    {
        _waitForThis = waitOverDelegate;
        this.forceBack = forBack;
    }
    
    public override void ProcessEnter()
    {
    }
    
    public override void ProcessEnd()
    {
    }
    
    public override bool CanEnterOtherProcess()
    {
        return _waitForThis();
    }

    public override void LocalUpdate()
    {
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
        }
        
        if (_returnLayer != null)
        {
            _returnLayer.gameObject.SetActive(true);
            if (forceBack)
                _returnLayer.ForceBackMode(true);
            else
                _returnLayer.HalfForceBackMode();
        }
    }
}

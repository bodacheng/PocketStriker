
public class WaitProcess : TutorialProcess
{
    public delegate bool WaitOverDelegate();

    readonly WaitOverDelegate _waitForThis;
    
    public WaitProcess(WaitOverDelegate waitOverDelegate)
    {
        _waitForThis = waitOverDelegate;
    }
        
    public override void ProcessEnd()
    {
    }
    
    public override bool CanEnterOtherProcess()
    {
        return _waitForThis();
    }
}
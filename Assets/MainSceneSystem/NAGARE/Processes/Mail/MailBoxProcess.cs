using DummyLayerSystem;
using mainMenu;

// 迎合PlayFab的机制我们是把邮件作为“item”去看待
// 邮箱top
public class MailBoxProcess : MSceneProcess
{
    MailBox _mailBox;
    public MailBoxProcess()
    {
        Step = MainSceneStep.MailBox;
    }
    
    public override void ProcessEnter()
    {
        _mailBox = UILayerLoader.Load<MailBox>();
        _mailBox.Setup();
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        upperInfoBar.Setup(null, null,null, null, PlayerAccountInfo.Me.noAdsState);
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<MailBox>();
        UILayerLoader.Remove<UpperInfoBar>();
    }
}

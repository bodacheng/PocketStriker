using mainMenu;
using DummyLayerSystem;

// 任何报酬的赋予都是服务端的工作，而反应在客户端上应该是一种根据远程结果进行刷新的机制

public class MailDetailProcess : MSceneProcess
{
    private string _mailId;
    
    public MailDetailProcess()
    {
        Step = MainSceneStep.MailDetail;
    }
    
    MailDetailView _mailDetailViewLayer;
    public override void ProcessEnter<String>(String id)
    {
        this._mailId = id.ToString();
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        upperInfoBar.Setup(null, null,null, null, PlayerAccountInfo.Me.noAdsState);
        _mailDetailViewLayer = UILayerLoader.Load<MailDetailView>();
        _mailDetailViewLayer.Setup(MailBox.LoadPic);
        var mail = PlayFabReadClient.Get(id.ToString());
        _mailDetailViewLayer.Read(mail);
        SetLoaded(true);
    }
    
    public override void ProcessEnter()
    {
        ProcessEnter(_mailId);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<MailDetailView>();
        UILayerLoader.Remove<UpperInfoBar>();
    }
}

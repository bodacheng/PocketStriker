using mainMenu;
using dataAccess;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;

public class FrontPage : MSceneProcess
{
    public FrontPage()
    {
        Step = MainSceneStep.FrontPage;
    }
    
    FrontLayer _frontLayer;
    bool _askedIfLinkDevice;
    
    void EnterProcess()
    {
        _frontLayer = UILayerLoader.Load<FrontLayer>();
        _frontLayer.Initialise(PreScene.target);

        string focusInstanceID = PreScene.target.GetRandomFocusInstanceID();
        PreScene.target.SetFocusingUnit(focusInstanceID);
        _frontLayer.ShowMyModel(focusInstanceID).Forget();
        
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        upperInfoBar.Setup(PlayerAccountInfo.Me.TitleDisplayName,
            () => PreScene.target.trySwitchToStep(MainSceneStep.Setting), 
            () => PreScene.target.trySwitchToStep(MainSceneStep.MailBox),
            () => PreScene.target.trySwitchToStep(MainSceneStep.ShopTop),
            PlayerAccountInfo.Me.noAdsState);
        
        // If account isn't linked to device, ask if link. Only ask once
        if (PlayerAccountInfo.Me.currentLinkedDeviceId != PlayFabReadClient.CustomId && !_askedIfLinkDevice)
        {
            _askedIfLinkDevice = true;
            var askIfLinkDeviceLayer = UILayerLoader.Load<AskIfLinkDeviceLayer>();
            askIfLinkDeviceLayer.Initialise(
                () =>
                {
                    PlayFabReadClient.LinkDevice(
                        () =>
                        {
                            PopupLayer.ArrangeWarnWindow(Translate.Get("AccountLinked"));
                            PlayerAccountInfo.Me.currentLinkedDeviceId = PlayFabReadClient.CustomId;
                        }
                    );
                    UILayerLoader.Remove<AskIfLinkDeviceLayer>();
                },
                () =>
                {
                    PopupLayer.ArrangeWarnWindow("U can link your account to this device later in setting.");
                    UILayerLoader.Remove<AskIfLinkDeviceLayer>();
                }
            );
        }
        
        if (PlayerAccountInfo.Me.tutorialProgress == "Finished" && Stones.TooManyStones())
        {
            _frontLayer.PlsClickBtn(MainSceneStep.SkillStoneList);
        }
        
        StoneLevelUpProccessor.CalUpdateAllForms();
        
        LowerMainBar.Open();
        SetLoaded(true);
    }
    
    public override void ProcessEnter()
    {
        PreScene.target.DataLoading(EnterProcess);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<FrontLayer>();
        UILayerLoader.Remove<UpperInfoBar>();
    }
}

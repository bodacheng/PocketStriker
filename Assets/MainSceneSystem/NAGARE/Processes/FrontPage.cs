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
    UpperInfoBar _upperInfoBar;
    bool _askedIfLinkDevice;
    
    void EnterProcess()
    {
        SetLoaded(false);
        _frontLayer = UILayerLoader.Load<FrontLayer>();
        _frontLayer.Initialise(PreScene.target);
        _frontLayer.OnBusyStateChanged = SetBusyState;

        string focusInstanceID = PreScene.target.GetRandomFocusInstanceID();
        PreScene.target.SetFocusingUnit(focusInstanceID);
        
        _upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        _upperInfoBar.Setup(PlayerAccountInfo.Me.TitleDisplayName,
            () => PreScene.target.trySwitchToStep(MainSceneStep.Setting), 
            () => PreScene.target.trySwitchToStep(MainSceneStep.MailBox),
            () => PreScene.target.trySwitchToStep(MainSceneStep.ShopTop),
            PlayerAccountInfo.Me.noAdsState);
        
        // If account isn't linked to device, ask if link. Only ask once
        if (PlayerAccountInfo.Me.currentLinkedDeviceId != PlayFabReadClient.CustomId && !_askedIfLinkDevice)
        {
            _askedIfLinkDevice = true;
            var askIfLinkDeviceLayer = UILayerLoader.Load<AskIfLinkDeviceLayer>(true, null, true);
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
                    PopupLayer.ArrangeWarnWindow(Translate.Get("CanLinkLater"));
                    UILayerLoader.Remove<AskIfLinkDeviceLayer>();
                }
            );
        }
        
        if (PlayerAccountInfo.Me.tutorialProgress == "Finished" && Stones.TooManyStones())
        {
            _frontLayer.PlsClickBtn(MainSceneStep.SkillStoneList);
        }
        
        StoneLevelUpProccessor.CalUpdateAllForms();

        UniTask.Void(async () =>
        {
            try
            {
                await _frontLayer.ShowMyModel(focusInstanceID);
            }
            finally
            {
                if (ProcessesRunner.Main.currentProcess == this)
                {
                    LowerMainBar.Open();
                    SetLoaded(true);
                }
            }
        });
    }

    void SetBusyState(bool busy)
    {
        _upperInfoBar?.SetInteractive(!busy);
    }
    
    public override void ProcessEnter()
    {
        PreScene.target.DataLoading(EnterProcess);
    }
    
    public override void ProcessEnd()
    {
        if (_frontLayer != null)
        {
            _frontLayer.OnBusyStateChanged = null;
        }
        UILayerLoader.Remove<FrontLayer>();
        UILayerLoader.Remove<UpperInfoBar>();
    }
}

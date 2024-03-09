using mainMenu;
using dataAccess;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;

public class FrontPage : MSceneProcess
{
    void StatisticsLoadFinished(bool value)
    {
        missionWatcher.Finish("statisticsFinished", value);
    }
    
    void MailCatalogFinished(bool value)
    {
        missionWatcher.Finish("mailCatalogFinished", value);
    }
    
    void UnitCatalogFinished(bool value)
    {
        missionWatcher.Finish("unitCatalogFinished", value);
    }
    
    void ItemsLoadFinished(bool value)
    {
        missionWatcher.Finish("itemsLoadFinished", value);
    }

    void StageRewardFinished(bool value)
    {
        missionWatcher.Finish("stageRewardsFinished", value);
    }
    
    void ArcadeTFinished(bool value)
    {
        missionWatcher.Finish("arcadeTFinished", value);
    }
    
    public FrontPage()
    {
        Step = MainSceneStep.FrontPage;
    }
    
    FrontLayer _frontLayer;
    bool _askedIfLinkDevice;

    void EnterProcess()
    {
        if (PlayerAccountInfo.Me.tutorialProgress == "Started")
        {
            var titleBgLayer = UILayerLoader.Load<TitleBgLayer>();
            titleBgLayer.Setup(1);
            titleBgLayer.Rotate(true, _EnterProcess);
        }
        else
        {
            _EnterProcess();
        }
    }
    
    void _EnterProcess()
    {
        TutorialRunner.Main.TutorialCheck();
        
        _frontLayer = UILayerLoader.Load<FrontLayer>();
        _frontLayer.Initialise(PreScene.target);
        
        string focusInstanceID;
        if (PreScene.target.Focusing != null && dataAccess.Units.Get(PreScene.target.Focusing.id) != null)
        {
            focusInstanceID = PreScene.target.Focusing.id;
        }
        else
        {
            focusInstanceID = PlayerPrefs.GetString("showUnit", null);
            if (string.IsNullOrEmpty(focusInstanceID) || dataAccess.Units.Get(focusInstanceID) == null)
            {
                foreach (var keyValuePair in dataAccess.Units.Dic)
                {
                    focusInstanceID = keyValuePair.Key;
                    break;
                }
            }
        }
        
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
                            PopupLayer.ArrangeWarnWindow("Account linked to device.");
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
            _frontLayer.PlsClickBtn("stones");
        }
        
        SSLevelUpManager.CalUpdateAllForms();
        
        LowerMainBar.Open(MainSceneStep.FrontPage);
        SetLoaded(true);
    }
    
    public override void ProcessEnter()
    {
        ProgressLayer.Loading(string.Empty);
        PlayFabReadClient.GetStatistics(StatisticsLoadFinished);
        
        //AccountCharsSet.LoadTutorial();
        PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._MailCatalog, MailCatalogFinished);
        PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._UnitCatalog, UnitCatalogFinished);
        PlayFabReadClient.LoadItems(ItemsLoadFinished);
        PlayFabReadClient.GetAllTitleData(StageRewardFinished);
        PlayFabReadClient.GetAllUserData( new List<string>(){"arcade", "gangbang", "noAds", PlayFabSetting._timeLimitBuyCode}, ArcadeTFinished);
        
        missionWatcher = new MissionWatcher(
            new List<string>
            {
                "mailCatalogFinished","unitCatalogFinished","itemsLoadFinished", 
                "statisticsFinished", "arcadeTFinished","stageRewardsFinished"
            },
            () =>
            {
                ProgressLayer.Close();
                EnterProcess();
            }
        );
        Stones.RenderAll().Forget(); // 在背后运行，从而加快石头列表和技能编辑画面的读取速度
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<FrontLayer>();
        UILayerLoader.Remove<UpperInfoBar>();
    }
}

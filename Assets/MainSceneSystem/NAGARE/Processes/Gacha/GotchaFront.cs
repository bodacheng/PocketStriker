using System;
using System.Collections.Generic;
using dataAccess;
using DummyLayerSystem;
using mainMenu;
using PlayFab.ClientModels;
using PlayFab;

public class GotchaFront : MSceneProcess
{
    private GotchaLayer _layer;
    
    public GotchaFront()
    {
        Step = MainSceneStep.GotchaFront;
    }
    
    private int _startIndex = 1;
    private Action _extraSuccessAction;
    public void SetExtraSuccessAction(Action extraSuccessAction)
    {
        this._extraSuccessAction = extraSuccessAction;
    }
    
    void MoveNext(int direction, List<DropTablePage> dropTables)
    {
        if (direction > 0)
        {
            _startIndex = _startIndex + 1;
        }
        else if (direction < 0)
        {
            _startIndex = _startIndex - 1;
        }

        if (_startIndex == dropTables.Count)
        {
            _startIndex = 0;
        }
        if (_startIndex < 0)
        {
            _startIndex = dropTables.Count - 1;
        }
        
        for (var i = 0; i < dropTables.Count; i++)
        {
            var dropTable = dropTables[i];
            if (_startIndex == i)
            {
                if (dropTable.ItemId == PlayFabSetting._GDGotchaCode)
                {
                    StarsFall.target.TriggerHoleEffect(StarsFall.GachaType.Normal);
                }
                
                if (dropTable.ItemId == PlayFabSetting._DMGotchaCode)
                {
                    StarsFall.target.TriggerHoleEffect(StarsFall.GachaType.Super);
                }
            }
            dropTable.Show(_startIndex == i);
        }
    }
     
    public override void ProcessEnter()
    {
        StarsFall.target.gameObject.SetActive(true);
        if (Stones.TooManyStones())
        {
            ReturnLayer.ReturnMissionList.Clear();
            PreScene.target.trySwitchToStep(MainSceneStep.FrontPage, false);
            return;
        }
        
        StarsFall.target.LookReset();
        BackGroundPS.target.Off();
        _layer = UILayerLoader.Load<GotchaLayer>();
        _layer.Setup(NineTimes, DropTableInfo, MoveNext, PlayerAccountInfo.Me.tutorialProgress != "Finished");
        
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        
        Action openDmShop = null;
        if (PlayerAccountInfo.Me.tutorialProgress == "Finished")
        {
            openDmShop = () =>
            {
                PreScene.target.trySwitchToStep(MainSceneStep.ShopTop);
            };
        }
        upperInfoBar.Setup(null, null,null, openDmShop, PlayerAccountInfo.Me.noAdsState);
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<GotchaLayer>();
        StarsFall.target.gameObject.SetActive(false);
    }
    
    void DropTableInfo(string dropTableId)
    {
        PreScene.target.trySwitchToStep(MainSceneStep.DropTableInfo, dropTableId, true);
    }

    private bool processingGotcha = false;
    void NineTimes(string itemId, string currencyCode, int currencyCount)
    {
        if (processingGotcha)
        {
            return;
        }
        processingGotcha = true;
        switch (currencyCode)
        {
            case "DM":
                if (Currencies.DiamondCount.Value < currencyCount)
                {
                    PreScene.target.trySwitchToStep(MainSceneStep.ShopTop);
                    processingGotcha = false;
                    return;
                }
                break;
            case "GD":
                if (Currencies.CoinCount.Value < currencyCount)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughGD"));
                    processingGotcha = false;
                    return;
                }
                break;
        }
        var returnLayer = UILayerLoader.Load<ReturnLayer>();
        if (returnLayer != null)
        {
            returnLayer.gameObject.SetActive(false);
        }
        UILayerLoader.Remove<UpperInfoBar>();
        UILayerLoader.Remove<GotchaLayer>();// 点击按钮瞬间关闭layer。
        PlayFabClientAPI.PurchaseItem(
            new PurchaseItemRequest
            {
                CatalogVersion = "stone",
                StoreId = "StoneGotcha",
                ItemId = itemId,
                VirtualCurrency = currencyCode,
                Price = currencyCount
            },
            (x) =>
            {
                var gotStones = new List<StoneOfPlayerInfo>();
                if (x.Items.Count > 0)
                {
                    foreach (var skillId in x.Items[0].BundleContents)
                    {
                        var stoneOfPlayerInfo = new StoneOfPlayerInfo
                        {
                            SkillId = skillId
                        };
                        gotStones.Add(stoneOfPlayerInfo);
                    }
                }
                
                PlayFabReadClient.LoadItems(null);
                
                GotchaResult.Result = gotStones;
                PreScene.target.trySwitchToStep(MainSceneStep.GotchaResult, itemId, true);
                _extraSuccessAction?.Invoke();
                processingGotcha = false;
            },
            (x) =>
            {
                PopupLayer.ArrangeWarnWindow(x.ErrorMessage);
                processingGotcha = false;
            });
    }
}

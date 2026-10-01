using mainMenu;
using dataAccess;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using PlayFab.ClientModels;
using PlayFab;
using System;

public class GotchaResult : MSceneProcess
{
    public static List<StoneOfPlayerInfo> Result;
    private string gotchaId;
    private GotchaResultLayer layer;
    
    public GotchaResult()
    {
        Step = MainSceneStep.GotchaResult;
    }
    
    public override void ProcessEnter<T>(T gotchaId)
    {
        this.gotchaId = gotchaId as string;
        layer = UILayerLoader.Load<GotchaResultLayer>();
        layer.Setup(this.gotchaId, NineTimes);
        StarsFall.target.Turn(true);
        layer.NineForShow.AddOnClickToSlots(layer.ShowDetail);
        layer.WholeAnimProcess(Result).Forget();
        
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        _purchaseControls.Restore();
        StarsFall.target.Turn(false);
        GotchaResultLayer.Close();
        StoneLevelUpProccessor.CalUpdateAllForms();
    }
    
    private bool processingGotcha = false;
    private readonly GachaPurchaseControls _purchaseControls = new GachaPurchaseControls();
    // Keep the offline request seam scoped to this process instance.
    private Action<PurchaseItemRequest, Action<PurchaseItemResult>, Action<PlayFabError>> _purchaseItem = RequestPurchase;

    private static void RequestPurchase(PurchaseItemRequest request, Action<PurchaseItemResult> success, Action<PlayFabError> failure)
    {
        PlayFabClientAPI.PurchaseItem(request, success, failure);
    }

    private void PurchaseFailed(string message)
    {
        _purchaseControls.Restore();
        processingGotcha = false;
        PopupLayer.ArrangeWarnWindow(message);
    }
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
        _purchaseControls.Pause(layer);
        _purchaseControls.Pause(UILayerLoader.Get<UpperInfoBar>());
        _purchaseControls.Pause(UILayerLoader.Get<ReturnLayer>());
        try
        {
            _purchaseItem(
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
                    _purchaseControls.Restore();
                    var returnLayer = UILayerLoader.Get<ReturnLayer>();
                    if (returnLayer != null) returnLayer.gameObject.SetActive(false);
                    UILayerLoader.Remove<UpperInfoBar>();
                    UILayerLoader.Remove<GotchaLayer>();
                    var gotStones = new List<StoneOfPlayerInfo> ();
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
                    PreScene.target.trySwitchToStep(MainSceneStep.GotchaResult, itemId, false);
                    processingGotcha = false;
                },
                (x) =>
                {
                    PurchaseFailed(x.ErrorMessage);
                });
        }
        catch (Exception exception)
        {
            PurchaseFailed(exception.Message);
        }
    }
}

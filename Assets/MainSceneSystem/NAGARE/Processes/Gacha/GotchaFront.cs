using System;
using System.Collections.Generic;
using dataAccess;
using DummyLayerSystem;
using mainMenu;
using PlayFab.ClientModels;
using PlayFab;
using UnityEngine;

public class GotchaFront : MSceneProcess
{
    private GotchaLayer _layer;
    
    public GotchaFront()
    {
        Step = MainSceneStep.GotchaFront;
    }
    
    private int _startIndex = 1;
    private Action<Action> _extraSuccessAction;
    public void SetExtraSuccessAction(Action<Action> extraSuccessAction)
    {
        this._extraSuccessAction = extraSuccessAction;
    }
    
    void MoveNext(int direction, List<DropTablePage> dropTables)
    {
        if (direction > 0)
        {
            _startIndex += 1;
        }
        else if (direction < 0)
        {
            _startIndex -= 1;
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
        StarsFall.target.Turn(true);
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
        _purchaseControls.Restore();
        UILayerLoader.Remove<GotchaLayer>();
        StarsFall.target.Turn(false);
    }
    
    void DropTableInfo(string dropTableId)
    {
        PreScene.target.trySwitchToStep(MainSceneStep.DropTableInfo, dropTableId, true);
    }

    private bool _processingGotcha = false;
    // Instance-scoped request seam lets offline validation use the real UI and
    // failure callback without contacting PlayFab or spending any currency.
    private Action<PurchaseItemRequest, Action<PurchaseItemResult>, Action<PlayFabError>> _purchaseItem = RequestPurchase;
    private readonly GachaPurchaseControls _purchaseControls = new GachaPurchaseControls();

    private static void RequestPurchase(PurchaseItemRequest request, Action<PurchaseItemResult> success, Action<PlayFabError> failure)
    {
        PlayFabClientAPI.PurchaseItem(request, success, failure);
    }

    private void PurchaseFailed(string message)
    {
        _purchaseControls.Restore();
        _processingGotcha = false;
        PopupLayer.ArrangeWarnWindow(message);
    }

    void NineTimes(string itemId, string currencyCode, int currencyCount)
    {
        if (_processingGotcha)
        {
            return;
        }
        _processingGotcha = true;
        switch (currencyCode)
        {
            case "DM":
                if (Currencies.DiamondCount.Value < currencyCount)
                {
                    PreScene.target.trySwitchToStep(MainSceneStep.ShopTop);
                    _processingGotcha = false;
                    return;
                }
                break;
            case "GD":
                if (Currencies.CoinCount.Value < currencyCount)
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughGD"));
                    _processingGotcha = false;
                    return;
                }
                break;
        }
        // Keep the selected page, balances and return control visible while the
        // request is pending. A failed request must not strand the player.
        _purchaseControls.Pause(_layer);
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

                    void Next()
                    {
                        PlayFabReadClient.LoadItems(null);
                        GotchaResult.Result = gotStones;
                        PreScene.target.trySwitchToStep(MainSceneStep.GotchaResult, itemId, true);
                        _processingGotcha = false;
                    }

                    if (_extraSuccessAction != null)
                    {
                        _extraSuccessAction.Invoke(Next);
                    }
                    else
                    {
                        Next();
                    }
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

// A request pauses existing controls rather than destroying the player's page.
// Restoring the captured state preserves controls that were already unavailable.
internal sealed class GachaPurchaseControls
{
    private sealed class ControlState
    {
        public CanvasGroup group;
        public bool interactable;
    }
    private readonly List<ControlState> states = new List<ControlState>();

    public void Pause(UILayer layer)
    {
        if (layer == null) return;
        var group = layer.GetComponent<CanvasGroup>();
        if (group == null) group = layer.gameObject.AddComponent<CanvasGroup>();
        states.Add(new ControlState { group = group, interactable = group.interactable });
        group.interactable = false;
    }

    public void Restore()
    {
        foreach (var state in states)
            if (state.group != null) state.group.interactable = state.interactable;
        states.Clear();
    }
}

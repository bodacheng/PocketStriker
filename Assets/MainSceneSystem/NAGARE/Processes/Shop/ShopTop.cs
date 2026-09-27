using System;
using DummyLayerSystem;
using mainMenu;
using System.Linq;
using System.Collections.Generic;

public partial class ShopTop : MSceneProcess
{
    private ShopTopLayer shopTopLayer;
    private bool isActive;
    private int catalogRequestVersion;
    public ShopTop()
    {
        Step = MainSceneStep.ShopTop;
    }
    
    public override void ProcessEnter()
    {
        isActive = true;
        BackGroundPS.target.ChangeBGByElement(Element.Null);
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        upperInfoBar.Setup(null,
            null, 
            null,
            null,
            PlayerAccountInfo.Me.noAdsState);
        
        shopTopLayer = UILayerLoader.Load<ShopTopLayer>();
        shopTopLayer.Initialize();
        shopTopLayer.ShowTimeLimitedBundle();
        // The shop remains navigable while catalogs and purchase history load.
        IAPManager.StoneProductCatalogChanged -= RefreshStoneBundles;
        IAPManager.StoneProductCatalogChanged += RefreshStoneBundles;
        SetLoaded(true);
        RefreshStoneBundles();
    }

    private void RefreshStoneBundles()
    {
        if (!isActive)
            return;

        var requestVersion = ++catalogRequestVersion;
        shopTopLayer.ShowStoneBundle(new List<string>());
        var productIds = IAPManager.StoneProductCatalog
            .Where(item => item != null && !string.IsNullOrEmpty(item.ItemId))
            .Select(item => item.ItemId).Distinct().ToList();
        if (productIds.Count == 0)
            return;

        bool IsCurrentRequest() => isActive && catalogRequestVersion == requestVersion;
        PlayFabReadClient.GetAllReadOnlyUserData(productIds, succeeded =>
        {
            if (IsCurrentRequest() && succeeded)
                shopTopLayer.ShowStoneBundle(PlayFabReadClient.ShowStoneBundleIds);
        }, IsCurrentRequest);
    }
    
    public override void ProcessEnd()
    {
        isActive = false;
        catalogRequestVersion++;
        IAPManager.StoneProductCatalogChanged -= RefreshStoneBundles;
        UILayerLoader.Remove<UpperInfoBar>();
        UILayerLoader.Remove<ShopTopLayer>();
        shopTopLayer = null;
    }

    public static bool HasTimeLimitSale(TimeLimitedBuyData data)
    {
        if (data == null || !TimeLimitedSaleWindow.TryGetActiveEndUtc(data.startTime, data.endTime, DateTime.UtcNow, out _))
        {
            return false;
        }

        bool on;
        
        PlayFabReadClient.MyTimeLimitBundleBoughtLog.TryGetValue(PlayFabSetting._timeLimitBuyCode, out var eventId);
        
        if (eventId == null) // 完全没买过
        {
            on = true;
        }
        else //  买过
        {
            if (eventId != data.eventID) 
            {
                on = true;
            }
            else // 已经在本活动期间买过
            {
                on = false;
            }
        }

        return on;
    }
}

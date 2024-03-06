using System;
using DummyLayerSystem;
using mainMenu;
using System.Linq;

public partial class ShopTop : MSceneProcess
{
    private ShopTopLayer shopTopLayer;
    public ShopTop()
    {
        Step = MainSceneStep.ShopTop;
    }
    
    public override void ProcessEnter()
    {
        var upperInfoBar = UILayerLoader.Load<UpperInfoBar>();
        upperInfoBar.Setup(null,
            null, 
            null,
            null,
            PlayerAccountInfo.Me.noAdsState);
        
        var stoneCatalog = IAPManager.StoneProductCatalog;
        var productIds = stoneCatalog.Select(x=> x.ItemId).ToList();
        PlayFabReadClient.GetAllReadOnlyUserData(productIds, (x) =>
        {
            if (x)
            {
                shopTopLayer = UILayerLoader.Load<ShopTopLayer>();
                shopTopLayer.Initialize();
                shopTopLayer.ShowStoneBundle(PlayFabReadClient.ShowStoneBundleIds);
                shopTopLayer.ShowTimeLimitedBundle();
            }
            SetLoaded(true);
        });
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<UpperInfoBar>();
        UILayerLoader.Remove<ShopTopLayer>();
    }

    public static bool HasTimeLimitSale(TimeLimitedBuyData data)
    {
        if (data == null)
        {
            return false;
        }
        
        DateTime startTime = DateTime.Parse(data.startTime);
        DateTime endTime = DateTime.Parse(data.endTime);
        DateTime currentTime = DateTime.UtcNow;
        bool on = currentTime >= startTime && currentTime <= endTime;
        
        if (!on)
        {
            return on;
        }
        
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
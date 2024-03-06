using mainMenu;
using dataAccess;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using PlayFab.ClientModels;
using PlayFab;

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
        StarsFall.target.gameObject.SetActive(true);
        layer.NineForShow.AddOnClickToSlots(layer.ShowDetail);
        layer.WholeAnimProcess(Result).Forget();
        
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        StarsFall.target.gameObject.SetActive(false);
        GotchaResultLayer.Close();
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
                PopupLayer.ArrangeWarnWindow(x.ErrorMessage);
                processingGotcha = false;
            });
    }
}

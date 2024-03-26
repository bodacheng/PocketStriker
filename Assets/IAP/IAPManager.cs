using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;
using DummyLayerSystem;
using mainMenu;
using UniRx;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class IAPManager : MonoBehaviour, IDetailedStoreListener {

    public static IAPManager Target;
    // The Unity Purchasing system
    private static IStoreController _mStoreController;
    // Items list, configurable via inspector
    private static List<CatalogItem> _productCatalog;
    private static List<CatalogItem> _stoneProductCatalog;
    private string noAdsServiceName = "vip_v";
    private string productClassName = "Product";
    private string ProductCatalogVersion = "Product";
    private string StoneProductCatalogVersion = "stone";
    private bool productCatalogInitialised = false;
    private bool stoneCatalogInitialised = false;

    public static List<CatalogItem> StoneProductCatalog => _stoneProductCatalog;

    string ProductCatalog(string productId)
    {
        var productProductIds = _productCatalog.Select(x=> x.ItemId).ToList();
        var stoneProductIds = _stoneProductCatalog.Select(x=> x.ItemId).ToList();
        if (productProductIds.Contains(productId))
        {
            return ProductCatalogVersion;
        }
        if (stoneProductIds.Contains(productId))
        {
            return StoneProductCatalogVersion;
        }
        
        if (productId == noAdsServiceName)
        {
            return ProductCatalogVersion;
        }
        return null;
    }
    
    private bool StoneProductCatalogInitialised
    {
        get => stoneCatalogInitialised;
        set {
            stoneCatalogInitialised = value;
            if (productCatalogInitialised && stoneCatalogInitialised)
            {
                InitializePurchasing();
            }
        }
    }
    
    private bool ProductCatalogInitialised
    {
        get => productCatalogInitialised;
        set {
            productCatalogInitialised = value;
            if (productCatalogInitialised && stoneCatalogInitialised)
            {
                InitializePurchasing();
            }
        }
    }
    
    // Bootstrap the whole thing
    public void Start() {
        // Make PlayFab log in
        Target = this;
        RefreshIAPItems();
    }
    
    void RefreshIAPItems() {
        
        if (IsInitialized.Value)
            return;
        PlayFabClientAPI.GetCatalogItems(
            new GetCatalogItemsRequest
            {
                CatalogVersion = ProductCatalogVersion
            },
        result => {
                _productCatalog = result.Catalog;
                // Make UnityIAP initialize
                ProductCatalogInitialised = true;
            }, 
        error => Debug.LogError(error.GenerateErrorReport())
        );
        
        PlayFabClientAPI.GetCatalogItems(
            new GetCatalogItemsRequest
            {
                CatalogVersion = StoneProductCatalogVersion
            },
            result => {
                _stoneProductCatalog = result.Catalog.FindAll(x=>x.ItemClass == ProductCatalogVersion);
                // Make UnityIAP initialize
                StoneProductCatalogInitialised = true;
            },
            error => Debug.LogError(error.GenerateErrorReport())
        );
    }

    public string GetProductLocalPriceString(string productId)
    {
        var productInfo = _mStoreController.products.WithID(productId);
        if (productInfo != null)
            return productInfo.metadata.localizedPriceString;
        return "Not Available";
    }

    // This is invoked manually on Start to initialize UnityIAP
    void InitializePurchasing() {
        // If IAP is already initialized, return gently
        
        if (IsInitialized.Value) return;
        
#if UNITY_IOS
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.AppleAppStore));
#endif

#if UNITY_ANDROID
        // Create a builder for IAP service
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
#endif
        // Register each item from the catalog
        foreach (var item in _productCatalog) {
            if (item.ItemClass == productClassName)
            {
                Debug.Log(productClassName + ":"+ item.ItemId);
                builder.AddProduct(item.ItemId, ProductType.Consumable);
            }
        }
        
        foreach (var item in _stoneProductCatalog) {
            if (item.ItemClass == productClassName)
            {
                Debug.Log(productClassName + ":"+ item.ItemId);
                builder.AddProduct(item.ItemId, ProductType.Consumable);
            }
        }
        
        builder.AddProduct(noAdsServiceName, ProductType.Consumable);
        
        // Trigger IAP service initialization
        UnityPurchasing.Initialize(this, builder);
    }

    void InitializePurchasingNoAds()
    {
#if UNITY_IOS
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.AppleAppStore));
#endif

#if UNITY_ANDROID
        // Create a builder for IAP service
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
#endif
        
        builder.AddProduct(noAdsServiceName, ProductType.Consumable);
        
        // Trigger IAP service initialization
        UnityPurchasing.Initialize(this, builder);
    }

    // We are initialized when StoreController and Extensions are set and we are logged in
    public ReactiveProperty<bool> IsInitialized = new ReactiveProperty<bool>();

    // This is automatically invoked automatically when IAP service is initialized
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions) {
        Debug.Log("Initialized ：" + controller);
        IsInitialized.SetValueAndForceNotify(true);
        _mStoreController = controller;
    }

    // This is automatically invoked automatically when IAP service failed to initialized
    public void OnInitializeFailed(InitializationFailureReason error) {
        Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
        Debug.Log("error message:" + message);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.Log("failureDescription " + failureDescription.productId + " : " + failureDescription.reason);
        ProgressLayer.Close();
    }

    // This is automatically invoked automatically when purchase failed
    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason) {
        Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
        ProgressLayer.Close();
    }
    
    // This is invoked automatically when successful purchase is ready to be processed
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e) {
        // NOTE: this code does not account for purchases that were pending and are
        // delivered on application start.
        // Production code should account for such case:
        // More: https://docs.unity3d.com/ScriptReference/Purchasing.PurchaseProcessingResult.Pending.html

        Debug.Log("ProcessPurchase");
        
        if (!IsInitialized.Value) {
            return PurchaseProcessingResult.Complete;
        }
        
        // Test edge case where product is unknown
        if (e.purchasedProduct == null) {
            Debug.LogWarning("Attempted to process purchase with unknown product. Ignoring");
            return PurchaseProcessingResult.Complete;
        }

        // Test edge case where purchase has no receipt
        if (string.IsNullOrEmpty(e.purchasedProduct.receipt)) {
            Debug.LogWarning("Attempted to process purchase with no receipt: ignoring");
            return PurchaseProcessingResult.Complete;
        }
        
        var boughtItemCatalog = ProductCatalog(e.purchasedProduct.definition.id);
        
        void ValidateSuccess()
        {
            switch (e.purchasedProduct.definition.id)
            {
                case "vip_v":
                    PopupLayer.ArrangeWarnWindow(Translate.Get("PurchaseVIPSuccess"));
                    break;
                case "beginnerbundle1_v":
                case "beginnerbundle2_v":
                case "beginnerbundle3_v":
                    PopupLayer.ArrangeWarnWindow(Translate.Get("PurchaseStoneBundleSuccess"));
                    break;
                default:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("PurchaseSuccess"));
                    break;
            }
            
            _mStoreController.ConfirmPendingPurchase(e.purchasedProduct);
            if (boughtItemCatalog == StoneProductCatalogVersion)
            {
                CloudScript.BoughtBundle(
                    e.purchasedProduct.definition.id,
                    () =>
                    {
                        var shopTopLayer = UILayerLoader.Get<ShopTopLayer>();
                        if (shopTopLayer != null)
                            shopTopLayer.DisableStoneBundle(e.purchasedProduct.definition.id);
                        ProgressLayer.Close();
                    }
                );
            }
            else if (e.purchasedProduct.definition.id == noAdsServiceName)
            {
                // 收据验证成功，调用Cloud Script来设置IsVIP标志
                PlayFabClientAPI.ExecuteCloudScript(
                    new ExecuteCloudScriptRequest
                    {
                        FunctionName = "setNoAdsStatus", // 云脚本的函数名
                        FunctionParameter = new { isVerified = true }, // 传递给云脚本的参数
                    }, 
                    (x) =>
                    {
                        var jsonResult = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(PlayFab.Json.PlayFabSimpleJson.SerializeObject(x.FunctionResult));
                        if (jsonResult.ContainsKey("rewardDM"))
                        {
                            int reward = Convert.ToInt32(jsonResult["rewardDM"]);
                            if (reward > 0)
                            {
                                Currencies.DiamondCount.Value += reward;
                            }
                        }
                        else
                        {
                            Debug.LogError("Reward not found in cloud script result");
                        }
                        var buyNoAdsLayer = UILayerLoader.Get<BuyNoAds>();
                        if (buyNoAdsLayer != null)
                            UILayerLoader.Remove<BuyNoAds>();

                        var arenaFightOver = UILayerLoader.Get<ArenaFightOver>();
                        if (arenaFightOver != null)
                            arenaFightOver.AdBtnParent.gameObject.SetActive(false);
                        
                        PlayerAccountInfo.Me.noAdsState = true;
                        var shopTopLayer = UILayerLoader.Get<ShopTopLayer>();
                        if (shopTopLayer != null)
                            shopTopLayer.ShowNoAdsProduct();
                        
                        ProgressLayer.Close();
                    }, 
                    y =>
                    {
                        Debug.Log(y);
                        ProgressLayer.Close();
                    }
                );
            }
            else if (e.purchasedProduct.definition.id == PlayFabSetting._timeLimitBuyCode)
            {
                PlayFabReadClient.UpdateUserData(
                    new UpdateUserDataRequest()
                    {
                        Data = new Dictionary<string, string>()
                        {
                            { PlayFabSetting._timeLimitBuyCode, PlayFabReadClient.TimeLimitedBuyData.eventID }
                        }
                    },
                    () =>
                    {
                        DicAdd<string,string>.Add(PlayFabReadClient.MyTimeLimitBundleBoughtLog, PlayFabSetting._timeLimitBuyCode, PlayFabReadClient.TimeLimitedBuyData.eventID);
                        var shopTopLayer = UILayerLoader.Get<ShopTopLayer>();
                        if (shopTopLayer != null)
                            shopTopLayer.ShowTimeLimitedBundle();
                        ProgressLayer.Close();
                    }
                );
            }
            else{
                ProgressLayer.Close();
            }
            PlayFabReadClient.LoadItems(null);
        }

        void ValidateFail(PlayFabError error)
        {
            ProgressLayer.Close();
            PopupLayer.ArrangeWarnWindow(Translate.Get("PurchaseFail"));
            Debug.Log("Validation failed: " + error.GenerateErrorReport());
        }
        
        #if UNITY_IOS
        var wrapper = (Dictionary<string, object>)MiniJson.JsonDecode(e.purchasedProduct.receipt);
        var store = (string)wrapper["Store"];
        var payload = (string)wrapper["Payload"]; // For Apple this will be the base64 encoded ASN.1 receipt

        //Debug.Log("CurrencyCode:"+e.purchasedProduct.metadata.isoCurrencyCode);
        //Debug.Log("PurchasePrice:"+(int)e.purchasedProduct.metadata.localizedPrice);

        var validateIOSReceiptRequest = new ValidateIOSReceiptRequest
        {
            CatalogVersion = boughtItemCatalog,
            CurrencyCode = e.purchasedProduct.metadata.isoCurrencyCode,
            PurchasePrice = (int)(e.purchasedProduct.metadata.localizedPrice * 100), //(int)e.purchasedProduct.metadata.localizedPrice * DMAmount(e.purchasedProduct.definition.id),
            ReceiptData = payload
        };
        Debug.Log("CatalogVersion:"+ boughtItemCatalog);
        Debug.Log("CurrencyCode:"+ e.purchasedProduct.metadata.isoCurrencyCode);
        Debug.Log("PurchasePrice:"+ (int)(e.purchasedProduct.metadata.localizedPrice * 100));
        Debug.Log("ReceiptData:"+ payload);
        
        PlayFabClientAPI.ValidateIOSReceipt(
            validateIOSReceiptRequest,
            result => { ValidateSuccess();},
            ValidateFail
        );
        #endif
        
        #if UNITY_ANDROID
        // Deserialize receipt
        Debug.Log("e.purchasedProduct.receipt :" + e.purchasedProduct.receipt);
        var googleReceipt = GooglePurchase.FromJson(e.purchasedProduct.receipt);

        // Invoke receipt validation
        // This will not only validate a receipt, but will also grant player corresponding items
        // only if receipt is valid.

        var validateGooglePlayPurchase = new ValidateGooglePlayPurchaseRequest
        {
            CatalogVersion = boughtItemCatalog,
            // Pass in currency code in ISO format
            CurrencyCode = e.purchasedProduct.metadata.isoCurrencyCode,
            // Convert and set Purchase price
            PurchasePrice = (uint)(e.purchasedProduct.metadata.localizedPrice * 100),//(uint)(e.purchasedProduct.metadata.localizedPrice * DMAmount(e.purchasedProduct.definition.id)),
            // Pass in the receipt
            ReceiptJson = googleReceipt.PayloadData.json,
            // Pass in the signature
            Signature = googleReceipt.PayloadData.signature
        };
        
        PlayFabClientAPI.ValidateGooglePlayPurchase(
            validateGooglePlayPurchase, 
            result =>{ ValidateSuccess();},
            ValidateFail
        );
        #endif
        
        return PurchaseProcessingResult.Complete;
    }
        
    // This is invoked manually to initiate purchase
    public void BuyProductID(string productId) {
        // If IAP service has not been initialized, fail hard
        
        if (!IsInitialized.Value) throw new Exception("IAP Service is not initialized!");
        ProgressLayer.Loading(Translate.Get("PurchaseProcessing"));
        // Pass in the product id to initiate purchase
        _mStoreController.InitiatePurchase(productId);
    }
}

// The following classes are used to deserialize JSON results provided by IAP Service
// Please, note that JSON fields are case-sensitive and should remain fields to support Unity Deserialization via JsonUtilities
public class JsonData {
    // JSON Fields, ! Case-sensitive

    public string orderId;
    public string packageName;
    public string productId;
    public long purchaseTime;
    public int purchaseState;
    public string purchaseToken;
}

public class PayloadData {
    public JsonData JsonData;

    // JSON Fields, ! Case-sensitive
    public string signature;
    public string json;

    public static PayloadData FromJson(string json) {
        var payload = JsonUtility.FromJson<PayloadData>(json);
        payload.JsonData = JsonUtility.FromJson<JsonData>(payload.json);
        return payload;
    }
}

public class GooglePurchase {
    public PayloadData PayloadData;

    // JSON Fields, ! Case-sensitive
    public string Store;
    public string TransactionID;
    public string Payload;

    public static GooglePurchase FromJson(string json) {
        var purchase = JsonUtility.FromJson<GooglePurchase>(json);
        purchase.PayloadData = PayloadData.FromJson(purchase.Payload);
        return purchase;
    }
}
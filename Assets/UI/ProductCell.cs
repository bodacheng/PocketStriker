using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class ProductCell : MonoBehaviour
{
    [SerializeField] private string product_id;
    [SerializeField] private Text price;
    [SerializeField] private Button btn;

    public string productId => product_id;

    // The fallback also works with an older downloaded localization catalog.
    internal static string UnavailableLabel()
    {
        var localized = Translate.Get("StoreUnavailable");
        if (!string.IsNullOrEmpty(localized)) return localized;
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.Japanese: return "購入不可";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "暂不可用";
            default: return "Unavailable";
        }
    }

    void RefreshPrice(bool initialized)
    {
        bool available = initialized && IAPManager.Target != null && IAPManager.Target.CanPurchaseProduct(product_id);
        price.text = available ? IAPManager.Target.GetProductLocalPriceString(product_id) : UnavailableLabel();
        btn.interactable = available;
    }

    void Start()
    {
        price.resizeTextForBestFit = true;
        price.resizeTextMinSize = 24;
        price.resizeTextMaxSize = 38;
        price.horizontalOverflow = HorizontalWrapMode.Wrap;
        price.verticalOverflow = VerticalWrapMode.Truncate;
        price.alignment = TextAnchor.MiddleCenter;
        price.raycastTarget = false;
        RefreshPrice(false);
        btn.onClick.AddListener(() =>
        {
            if (IAPManager.Target != null && IAPManager.Target.CanPurchaseProduct(product_id))
                IAPManager.Target.BuyProductID(product_id);
        });
        Observable.EveryUpdate()
            .First(_ => IAPManager.Target != null)
            .SelectMany(_ => IAPManager.Target.IsInitialized)
            .Subscribe(RefreshPrice).AddTo(this);
    }
}

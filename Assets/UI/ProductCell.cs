using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class ProductCell : MonoBehaviour
{
    [SerializeField] private string product_id;
    [SerializeField] private Text price;
    [SerializeField] private Button btn;
    
    public string productId => product_id;
    
    void Start()
    {
        price.text = "Not Available";
        btn.interactable = false;
        btn.onClick.AddListener(() =>
        {
            if (IAPManager.Target != null && IAPManager.Target.CanPurchaseProduct(product_id))
                IAPManager.Target.BuyProductID(product_id);
        });
        Observable.EveryUpdate()
            .First(_ => IAPManager.Target != null)
            .SelectMany(_ => IAPManager.Target.IsInitialized)
            .Subscribe(initialized =>
            {
                price.text = initialized
                    ? IAPManager.Target.GetProductLocalPriceString(product_id)
                    : "Not Available";
                btn.interactable = IAPManager.Target.CanPurchaseProduct(product_id);
            }).AddTo(this);
    }
}

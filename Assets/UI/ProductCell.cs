using UnityEngine;
using UnityEngine.UI;

public class ProductCell : MonoBehaviour
{
    [SerializeField] private string product_id;
    [SerializeField] private Text price;
    [SerializeField] private Button btn;
    
    public string productId => product_id;
    
    void Start()
    {
        price.text = IAPManager.Target.GetProductLocalPriceString(product_id).ToString();
        btn.onClick.AddListener(()=> IAPManager.Target.BuyProductID(product_id));
    }
}

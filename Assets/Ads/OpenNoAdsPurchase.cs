using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public class OpenNoAdsPurchase : MonoBehaviour
{
    public void OpenNoAds()
    {
        UILayerLoader.Load<BuyNoAds>();
    }

    public void RemoveNoAds()
    {
        UILayerLoader.Remove<BuyNoAds>();
    }
}

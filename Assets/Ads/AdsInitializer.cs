using UnityEngine;
using GoogleMobileAds.Api;
using UnityEngine.PlayerLoop;

public class AdsInitializer : MonoBehaviour
{
    public static AdsInitializer target;

    public bool Initialized
    {
        get;
        set;
    }
    
    void Awake()
    {
        target = this;
        InitializeAds();
    }
    
    void InitializeAds()
    {
        // Google admob
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("谷歌广告插件初始化状态："+initStatus.getAdapterStatusMap());
            Initialized = true;
            if (BannerAds.target != null)
            {
                if (BannerAds.target.BannerView == null)
                {
                    BannerAds.target.LoadAd();
                }
            }
        });
    }
}
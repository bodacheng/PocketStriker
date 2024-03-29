using UnityEngine;
using GoogleMobileAds.Api;

public class AdsInitializer : MonoBehaviour
{
    [SerializeField] private BannerAds bannerAds;
    void Awake()
    {
        InitializeAds();
    }
    
    void InitializeAds()
    {
        // Google admob
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("谷歌广告插件初始化状态："+initStatus.getAdapterStatusMap());
            bannerAds?.LoadAd();
        });
    }
}
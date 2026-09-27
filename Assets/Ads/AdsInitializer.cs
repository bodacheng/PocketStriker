using UnityEngine;
using GoogleMobileAds.Api;

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
        if (ShouldEnableAds())
            InitializeAds();
    }

    public static bool ShouldEnableAds()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer ||
               Application.platform == RuntimePlatform.Android;
    }

    void InitializeAds()
    {
        // Ad callbacks update Unity UI and must run on the Unity thread.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(initStatus =>
        {
            if (this == null)
                return;
            Debug.Log("谷歌广告插件初始化状态：" + initStatus);
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

using System;
using UnityEngine;
//using GoogleMobileAds.Api;
using UnityEngine.UI;

public class GoogleMobileAdsManager : MonoBehaviour
{
    [SerializeField] Button _showAdButton;
    
    // google admob
    //private InterstitialAd interstitial;
    
    private Func<bool> enableCondition;

    void Awake()
    {
        //_showAdButton.onClick.AddListener(RequestInterstitial);
    }

    public void SetEnableCondition(Func<bool> enableCondition)
    {
        this.enableCondition = enableCondition;
    }

    public void Enable(bool on)
    {
        _showAdButton.interactable = on;
    }
    
    /// <summary>
    /// google admob
    /// </summary>
//     public void RequestInterstitial()
//     {
// #if UNITY_ANDROID
//         string adUnitId = "ca-app-pub-3094747799359437/1800785761";
// #elif UNITY_IOS
//         string adUnitId = "ca-app-pub-3094747799359437/1421143280";
// #else
//         string adUnitId = "unexpected_platform";
// #endif
//         // Initialize an InterstitialAd.
//         this.interstitial = new InterstitialAd(adUnitId);
//         
//         // Called when an ad request has successfully loaded.
//         this.interstitial.OnAdLoaded += HandleOnAdLoaded;
//         // Called when an ad request failed to load.
//         this.interstitial.OnAdFailedToLoad += HandleOnAdFailedToLoad;
//         // Called when an ad is shown.
//         this.interstitial.OnAdOpening += HandleOnAdOpening;
//         // Called when the ad is closed.
//         this.interstitial.OnAdClosed += HandleOnAdClosed;
//         
//         // Create an empty ad request.
//         AdRequest request = new AdRequest.Builder().Build();
//         // Load the interstitial with the request.
//         this.interstitial.LoadAd(request);
//     }
    
    // public void HandleOnAdLoaded(object sender, EventArgs args)
    // {
    //     MonoBehaviour.print("HandleAdLoaded event received");
    //     if (this.interstitial.IsLoaded()) {
    //         this.interstitial.Show();
    //     }
    // }
    //
    // public void HandleOnAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
    // {
    //     MonoBehaviour.print("HandleFailedToReceiveAd event received with message: " + args.LoadAdError);
    // }
    //
    // public void HandleOnAdOpening(object sender, EventArgs args)
    // {
    //     MonoBehaviour.print("HandleAdOpening event received");
    // }
    //
    // public void HandleOnAdClosed(object sender, EventArgs args)
    // {
    //     MonoBehaviour.print("HandleAdClosed event received");
    //     interstitial.Destroy();
    //     CloudScript.SubtractVirtualCurrency(
    //         "AD",1,
    //         CloudScript.RequestAdReward
    //     );
    // }
}

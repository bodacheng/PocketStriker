using System;
using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds.Api;

public class AdmobAdsButton : MonoBehaviour
{
    [SerializeField] AdType adType;
    [SerializeField] Button _showAdButton;
    [SerializeField] bool reloadAfterWatched;
    [SerializeField] Text text;
    string _adUnitId = null; // This will remain null for unsupported platforms

    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;
    private Action watchedAdExtraProcess;

    [SerializeField] Image[] colorImages;

    public String Text
    {
        set => text.text = value;
    }
    
    public void SetWatchedAdExtraProcess(Action watchedAdProcess)
    {
        this.watchedAdExtraProcess = watchedAdProcess;
    }

    private bool adIsReady;

    public bool AdIsReady
    {
        get => adIsReady;
        set
        {
            adIsReady = value;
            _showAdButton.interactable = adIsReady && hasTicket;
            SetColor();
        }
    }

    private bool hasTicket;

    public bool HasTicket
    {
        get => hasTicket;
        set
        {
            hasTicket = value;
            _showAdButton.interactable = adIsReady && hasTicket;
            SetColor();
        }
    }

    void SetColor()
    {
        foreach (var image in colorImages)
        {
            var color = image.color;
            image.color = new Color(color.r, color.g, color.b, _showAdButton.interactable ? 1:0.5f);
        }
    }
    
    void Awake()
    {
        IniUnitId();
    }

    void IniUnitId()
    {
        // Get the Ad Unit ID for the current platform:
#if UNITY_IOS
        switch (adType)
        {
            case AdType.Interstitial:
                _adUnitId = CommonSetting.Admob_interstitial_iosKey;
                break;
            case AdType.Reward:
                _adUnitId = CommonSetting.Admob_rewarded_iosKey;
                break;
        }
#elif UNITY_ANDROID
        switch (adType)
        {
            case AdType.Interstitial:
                _adUnitId = CommonSetting.Admob_interstitial_androidKey;
                break;
            case AdType.Reward:
                _adUnitId = CommonSetting.rewarded_androidKey;
                break;
        }
#endif
    }
    
    // Implement a method to execute when the user clicks the button:
    public void ShowAd()
    {
        switch (adType)
        {
            case AdType.Interstitial:
                if (_interstitialAd != null && _interstitialAd.CanShowAd())
                {
                    _interstitialAd.Show();
                } 
                else
                {
                    Debug.LogError("Interstitial ad is not ready yet.");
                }
                break;
            case AdType.Reward:
                if (_rewardedAd != null && _rewardedAd.CanShowAd())
                {
                    _rewardedAd.Show((x) =>
                    {
                        watchedAdExtraProcess.Invoke();
                        if (reloadAfterWatched)
                            LoadRewardAd();
                        AppSetting.Value.UnMute();
                    });
                }
                else
                {
                    Debug.LogError("Rewarded ad is not ready yet.");
                }
                break;
        }
    }
    
    private void RegisterEventHandlers(InterstitialAd interstitialAd)
    {
        // Raised when the ad is estimated to have earned money.
        interstitialAd.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Rewarded interstitial ad paid {0} {1}.", adValue.Value, adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        interstitialAd.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded interstitial ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        interstitialAd.OnAdClicked += () =>
        {
            Debug.Log("Rewarded interstitial ad was clicked.");
        };
        
        // Raised when an ad opened full screen content.
        interstitialAd.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Rewarded interstitial ad full screen content opened.");
            AdIsReady = false;
            AppSetting.Value.Mute();
        };
        // Raised when the ad closed full screen content.
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded interstitial ad full screen content closed.");
            watchedAdExtraProcess.Invoke();
            // Load another ad: 需要检查在实机上这里跑的是否有问题。在editor上产生一个造成广告再次观看时连续跑了两次的错误
            if (reloadAfterWatched)
                LoadInterstitialAd();
            
            AppSetting.Value.UnMute();
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded interstitial ad failed to open " +
                           "full screen content with error : " + error);
        };
    }
    
    private void RegisterEventHandlers(RewardedAd rewardedAd)
    {
        // Raised when the ad is estimated to have earned money.
        rewardedAd.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Rewarded ad paid {0} {1}.", adValue.Value, adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        rewardedAd.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        rewardedAd.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked.");
        };
        
        // Raised when an ad opened full screen content.
        rewardedAd.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Rewarded ad full screen content opened.");
            AdIsReady = false;
            AppSetting.Value.Mute();
        };
        // Raised when the ad closed full screen content.
        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            // 相关处理放到_rewardedAd.Show那边？
            AppSetting.Value.UnMute();
        };
        // Raised when the ad failed to open full screen content.
        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open " +
                           "full screen content with error : " + error);
        };
    }

    public void LoadAd()
    {
        switch (adType)
        {
            case AdType.Interstitial:
                LoadInterstitialAd();
                break;
            case AdType.Reward:
                LoadRewardAd();
                break;
        }
    }
    
    /// <summary>
    /// Loads the rewarded interstitial ad.
    /// </summary>
    void LoadInterstitialAd()
    {
        // Clean up the old ad before loading a new one.
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        Debug.Log("Loading the rewarded interstitial ad.");
        
        // create our request used to load the ad.
        var adRequest = new AdRequest();
        //adRequest.Keywords.Add("unity-admob-sample");

        // send the request to load the ad.
        InterstitialAd.Load(_adUnitId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("rewarded interstitial ad failed to load an ad with error : " + error);
                    return;
                }
                
                Debug.Log("Rewarded interstitial ad loaded with response : " + ad.GetResponseInfo());
                
                _interstitialAd = ad;
                AdIsReady = true;
                RegisterEventHandlers(_interstitialAd);
            });
    }
    
    void LoadRewardAd()
    {
        // Clean up the old ad before loading a new one.
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        Debug.Log("Loading the reward ad.");
        
        // create our request used to load the ad.
        var adRequest = new AdRequest();
        //adRequest.Keywords.Add("unity-admob-sample");

        // send the request to load the ad.
        RewardedAd.Load(_adUnitId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad with error : " + error);
                    return;
                }
                
                Debug.Log("Rewarded ad loaded with response : " + ad.GetResponseInfo());
                
                _rewardedAd = ad;
                AdIsReady = true;
                RegisterEventHandlers(_rewardedAd);
            });
    }
    
    enum AdType
    {
        Interstitial = 0,
        Reward = 1
    }
}
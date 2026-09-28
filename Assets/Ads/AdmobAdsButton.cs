using System;
using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

public class AdmobAdsButton : MonoBehaviour
{
    [SerializeField] AdType adType;
    [SerializeField] Button _showAdButton;
    [SerializeField] bool reloadAfterWatched;
    [SerializeField] Text text;
    string _adUnitId = null; // This will remain null for unsupported platforms

    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;
    private Action _watchedAdExtraProcess;
    private bool _interstitialLoading;
    private bool _rewardedLoading;
    private bool _destroyed;
    private bool _waitingForAdsReady;

    private static void RunOnUnityThread(Action action)
    {
        MobileAdsEventExecutor.ExecuteInUpdate(action);
    }

    [SerializeField] Image[] colorImages;

    public Button ShowAdButton=>_showAdButton;

    public String Text
    {
        set => text.text = value;
    }

    public void SetWatchedAdExtraProcess(Action watchedAdProcess)
    {
        this._watchedAdExtraProcess = watchedAdProcess;
    }

    // The fight result prefab is a rewarded-ad button. The passive event-battle
    // placement uses a separate, hidden instance of that prefab as an interstitial.
    public void UseInterstitialAd()
    {
        adType = AdType.Interstitial;
        reloadAfterWatched = true;
        _watchedAdExtraProcess = null;
        IniUnitId();
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
                _adUnitId = Debug.isDebugBuild
                    ? "ca-app-pub-3940256099942544/4411468910"
                    : CommonSetting.Admob_interstitial_iosKey;
                break;
            case AdType.Reward:
                _adUnitId = Debug.isDebugBuild
                    ? "ca-app-pub-3940256099942544/1712485313"
                    : CommonSetting.Admob_rewarded_iosKey;
                break;
        }
#elif UNITY_ANDROID
        switch (adType)
        {
            case AdType.Interstitial:
                _adUnitId = Debug.isDebugBuild
                    ? "ca-app-pub-3940256099942544/1033173712"
                    : CommonSetting.Admob_interstitial_androidKey;
                break;
            case AdType.Reward:
                _adUnitId = Debug.isDebugBuild
                    ? "ca-app-pub-3940256099942544/5224354917"
                    : CommonSetting.Admob_rewarded_androidKey;
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
                    AdIsReady = false;
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
                    AdIsReady = false;
                    var rewardGranted = false;
                    var rewardAction = _watchedAdExtraProcess;
                    _rewardedAd.Show((x) =>
                    {
                        RunOnUnityThread(() =>
                        {
                            if (this == null || _destroyed || rewardGranted)
                                return;
                            rewardGranted = true;
                            rewardAction?.Invoke();
                        });
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
        var finished = false;
        // Raised when the ad is estimated to have earned money.
        interstitialAd.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Interstitial ad paid {0} {1}.", adValue.Value, adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        interstitialAd.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Interstitial ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        interstitialAd.OnAdClicked += () =>
        {
            Debug.Log("Interstitial ad was clicked.");
        };

        // Raised when an ad opened full screen content.
        interstitialAd.OnAdFullScreenContentOpened += () =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed)
                    return;
                Debug.Log("Interstitial ad full screen content opened.");
                AdIsReady = false;
                AppSetting.Value.Mute();
            });
        };
        // Raised when the ad closed full screen content.
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished)
                    return;
                finished = true;
                Debug.Log("Interstitial ad full screen content closed.");
                AdIsReady = false;
                if (_interstitialAd == interstitialAd)
                    _interstitialAd = null;
                interstitialAd.Destroy();
                if (reloadAfterWatched)
                    LoadInterstitialAd();

                AppSetting.Value.UnMute();
            });
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished)
                    return;
                finished = true;
                AdIsReady = false;
                if (_interstitialAd == interstitialAd)
                    _interstitialAd = null;
                interstitialAd.Destroy();
                AppSetting.Value.UnMute();
                Debug.LogError("Interstitial ad failed to open " +
                               "full screen content with error : " + error);
                LoadInterstitialAd();
            });
        };
    }

    private void RegisterEventHandlers(RewardedAd rewardedAd)
    {
        var finished = false;
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
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed)
                    return;
                Debug.Log("Rewarded ad full screen content opened.");
                AdIsReady = false;
                AppSetting.Value.Mute();
            });
        };
        // Raised when the ad closed full screen content.
        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished)
                    return;
                finished = true;
                AdIsReady = false;
                if (_rewardedAd == rewardedAd)
                    _rewardedAd = null;
                rewardedAd.Destroy();
                AppSetting.Value.UnMute();
                if (reloadAfterWatched)
                    LoadRewardAd();
            });
        };
        // Raised when the ad failed to open full screen content.
        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished)
                    return;
                finished = true;
                AdIsReady = false;
                if (_rewardedAd == rewardedAd)
                    _rewardedAd = null;
                rewardedAd.Destroy();
                AppSetting.Value.UnMute();
                Debug.LogError("Rewarded ad failed to open " +
                               "full screen content with error : " + error);
                LoadRewardAd();
            });
        };
    }

    public void LoadAd()
    {
        if (_destroyed)
            return;

        if (string.IsNullOrEmpty(_adUnitId))
            IniUnitId();

        if (!AdsInitializer.ShouldEnableAds() || string.IsNullOrEmpty(_adUnitId))
        {
            AdIsReady = false;
            return;
        }

        if (!AdsInitializer.IsReady)
        {
            AdIsReady = false;
            if (!_waitingForAdsReady)
            {
                _waitingForAdsReady = true;
                AdsInitializer.AdsReady += OnAdsReady;
            }
            if (AdsInitializer.IsReady)
                OnAdsReady();
            return;
        }

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

    private void OnAdsReady()
    {
        AdsInitializer.AdsReady -= OnAdsReady;
        _waitingForAdsReady = false;
        if (this != null && !_destroyed)
            LoadAd();
    }

    /// <summary>
    /// Loads the ordinary interstitial ad.
    /// </summary>
    void LoadInterstitialAd()
    {
        if (_interstitialLoading || (_interstitialAd != null && _interstitialAd.CanShowAd()))
            return;

        _interstitialLoading = true;
        AdIsReady = false;
        // Clean up the old ad before loading a new one.
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        Debug.Log("Loading the interstitial ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest();
        //adRequest.Keywords.Add("unity-admob-sample");

        // send the request to load the ad.
        InterstitialAd.Load(_adUnitId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                RunOnUnityThread(() =>
                {
                    if (this == null || _destroyed)
                    {
                        ad?.Destroy();
                        return;
                    }
                    _interstitialLoading = false;
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        ad?.Destroy();
                        Debug.LogError("Interstitial ad failed to load an ad with error : " + error);
                        return;
                    }

                    Debug.Log("Interstitial ad loaded with response : " + ad.GetResponseInfo());

                    _interstitialAd = ad;
                    AdIsReady = true;
                    RegisterEventHandlers(_interstitialAd);
                });
            });
    }

    void LoadRewardAd()
    {
        if (_rewardedLoading || (_rewardedAd != null && _rewardedAd.CanShowAd()))
            return;

        _rewardedLoading = true;
        AdIsReady = false;

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
                RunOnUnityThread(() =>
                {
                    if (this == null || _destroyed)
                    {
                        ad?.Destroy();
                        return;
                    }
                    _rewardedLoading = false;

                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        ad?.Destroy();
                        Debug.LogError("Rewarded ad failed to load an ad with error : " + error);
                        return;
                    }

                    Debug.Log("Rewarded ad loaded with response : " + ad.GetResponseInfo());

                    _rewardedAd = ad;
                    AdIsReady = true;
                    RegisterEventHandlers(_rewardedAd);
                });
            });
    }

    void OnDestroy()
    {
        _destroyed = true;
        AdsInitializer.AdsReady -= OnAdsReady;
        _interstitialAd?.Destroy();
        _rewardedAd?.Destroy();
    }

    enum AdType
    {
        Interstitial = 0,
        Reward = 1
    }
}

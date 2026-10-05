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
    private static AdmobAdsButton fullScreenAdOwner;

    public static bool IsFullScreenAdShowing => fullScreenAdOwner != null;

    private static void RunOnUnityThread(Action action)
    {
        MobileAdsEventExecutor.ExecuteInUpdate(action);
    }

    [SerializeField] Image[] colorImages = Array.Empty<Image>();

    public Button ShowAdButton=>_showAdButton;

    public String Text
    {
        set => text.text = value;
    }

    public void SetWatchedAdExtraProcess(Action watchedAdProcess)
    {
        this._watchedAdExtraProcess = watchedAdProcess;
    }

    // Automatic placements can use this lifecycle without any UI components.
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
            RefreshButton();
        }
    }

    private bool hasTicket;

    public bool HasTicket
    {
        get => hasTicket;
        set
        {
            hasTicket = value;
            RefreshButton();
        }
    }

    void RefreshButton()
    {
        if (_showAdButton == null) return;
        _showAdButton.interactable = adIsReady && hasTicket;
        if (colorImages == null) return;
        foreach (var image in colorImages)
        {
            if (image == null) continue;
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
        if (!TryShowAd())
            Debug.LogWarning("Ad is not ready, disabled, or another full screen ad is showing.");
    }

    public bool TryShowAd()
    {
        if (_destroyed || IsFullScreenAdShowing ||
            (PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState))
            return false;

        switch (adType)
        {
            case AdType.Interstitial:
                if (_interstitialAd != null && _interstitialAd.CanShowAd())
                {
                    return PresentAd(() => _interstitialAd.Show());
                }
                break;
            case AdType.Reward:
                if (_rewardedAd != null && _rewardedAd.CanShowAd())
                {
                    var rewardGranted = false;
                    var rewardAction = _watchedAdExtraProcess;
                    return PresentAd(() => _rewardedAd.Show((x) =>
                    {
                        RunOnUnityThread(() =>
                        {
                            if (this == null || _destroyed || rewardGranted)
                                return;
                            rewardGranted = true;
                            rewardAction?.Invoke();
                        });
                    }));
                }
                break;
        }
        return false;
    }

    private bool PresentAd(Action show)
    {
        fullScreenAdOwner = this;
        AdIsReady = false;
        try
        {
            show();
            return true;
        }
        catch (Exception error)
        {
            ReleaseFullScreenAd();
            AppSetting.Value.UnMute();
            if (adType == AdType.Interstitial)
            {
                var failedAd = _interstitialAd;
                _interstitialAd = null;
                failedAd?.Destroy();
            }
            else
            {
                var failedAd = _rewardedAd;
                _rewardedAd = null;
                failedAd?.Destroy();
            }
            Debug.LogWarning("Ad could not open: " + error.Message);
            LoadAd();
            return false;
        }
    }

    private void ReleaseFullScreenAd()
    {
        if (fullScreenAdOwner == this) fullScreenAdOwner = null;
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
                if (this == null || _destroyed || finished || _interstitialAd != interstitialAd)
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
                if (this == null || _destroyed || finished || _interstitialAd != interstitialAd)
                    return;
                finished = true;
                ReleaseFullScreenAd();
                Debug.Log("Interstitial ad full screen content closed.");
                AdIsReady = false;
                if (_interstitialAd == interstitialAd)
                    _interstitialAd = null;
                interstitialAd.Destroy();
                if (reloadAfterWatched)
                    LoadAd();

                AppSetting.Value.UnMute();
            });
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished || _interstitialAd != interstitialAd)
                    return;
                finished = true;
                ReleaseFullScreenAd();
                AdIsReady = false;
                if (_interstitialAd == interstitialAd)
                    _interstitialAd = null;
                interstitialAd.Destroy();
                AppSetting.Value.UnMute();
                Debug.LogError("Interstitial ad failed to open " +
                               "full screen content with error : " + error);
                LoadAd();
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
                if (this == null || _destroyed || finished || _rewardedAd != rewardedAd)
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
                if (this == null || _destroyed || finished || _rewardedAd != rewardedAd)
                    return;
                finished = true;
                ReleaseFullScreenAd();
                AdIsReady = false;
                if (_rewardedAd == rewardedAd)
                    _rewardedAd = null;
                rewardedAd.Destroy();
                AppSetting.Value.UnMute();
                if (reloadAfterWatched)
                    LoadAd();
            });
        };
        // Raised when the ad failed to open full screen content.
        rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnUnityThread(() =>
            {
                if (this == null || _destroyed || finished || _rewardedAd != rewardedAd)
                    return;
                finished = true;
                ReleaseFullScreenAd();
                AdIsReady = false;
                if (_rewardedAd == rewardedAd)
                    _rewardedAd = null;
                rewardedAd.Destroy();
                AppSetting.Value.UnMute();
                Debug.LogError("Rewarded ad failed to open " +
                               "full screen content with error : " + error);
                LoadAd();
            });
        };
    }

    public void LoadAd()
    {
        if (_destroyed || (PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState))
        {
            AdIsReady = false;
            return;
        }
        // Never replace an SDK instance whose full screen presentation is live.
        if (fullScreenAdOwner == this) return;

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
        if (fullScreenAdOwner == this)
        {
            ReleaseFullScreenAd();
            AppSetting.Value.UnMute();
        }
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

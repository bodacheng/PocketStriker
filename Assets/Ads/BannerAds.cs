using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using System;

public class BannerAds : MonoBehaviour
{
    public static BannerAds target;
    private string _adUnitId;
    float _occupiedHeightPixels;
    bool _hasLoaded;
    Vector2Int _requestedScreen;
    Rect _requestedSafeArea;
    public static float OccupiedHeightPixels => target != null && target.isActiveAndEnabled
        ? target._occupiedHeightPixels : 0;
    public static event Action OccupiedAreaChanged;

    void SetOccupiedHeight(float pixels)
    {
        pixels = float.IsNaN(pixels) || float.IsInfinity(pixels) ? 0 : Mathf.Max(0, pixels);
        if (Mathf.Approximately(_occupiedHeightPixels, pixels)) return;
        _occupiedHeightPixels = pixels;
        OccupiedAreaChanged?.Invoke();
    }

    void RefreshOccupiedArea()
    {
        SetOccupiedHeight(_hasLoaded && _bannerView != null ? _bannerView.GetHeightInPixels() : 0);
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        target = null;
        OccupiedAreaChanged = null;
    }

    void Awake()
    {
        target = this;
        IniUnitId();
    }

    void OnEnable()
    {
        AdsInitializer.AdsReady += LoadAd;
        if (AdsInitializer.IsReady)
            LoadAd();
    }

    void Start()
    {
        if (AdsInitializer.IsReady && _bannerView == null)
            LoadAd();
    }

    void OnDisable()
    {
        AdsInitializer.AdsReady -= LoadAd;
        DestroyBannerView();
    }

    void OnDestroy()
    {
        DestroyBannerView();
        if (target == this)
            target = null;
    }

    void IniUnitId()
    {
        // Get the Ad Unit ID for the current platform:
#if UNITY_IOS
        _adUnitId = Debug.isDebugBuild
            ? "ca-app-pub-3940256099942544/2435281174"
            : CommonSetting.Admob_banner_iosKey;
#elif UNITY_ANDROID
        _adUnitId = Debug.isDebugBuild
            ? "ca-app-pub-3940256099942544/9214589741"
            : CommonSetting.Admob_banner_androidKey;
#endif
    }

    BannerView _bannerView;
    public BannerView BannerView => _bannerView;

    /// <summary>
    /// Creates a current-orientation adaptive banner at the top left.
    /// </summary>
    public void CreateBannerView()
    {
        if (!AdsInitializer.IsReady || PlayerAccountInfo.Me == null ||
            PlayerAccountInfo.Me.noAdsState || string.IsNullOrEmpty(_adUnitId))
            return;
        // If we already have a banner, destroy the old one.
        if (_bannerView != null)
        {
            DestroyBannerView();
        }

        // Use the AdSize argument to set a custom size for the ad.

        var adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(200);
        // Debug.Log(adSize.Width + ":"+ adSize.Height);
        _requestedScreen = new Vector2Int(Screen.width, Screen.height);
        _requestedSafeArea = Screen.safeArea;
        _bannerView = new BannerView(_adUnitId, adSize, AdPosition.TopLeft);
        ListenToAdEvents();
    }

    /// <summary>
    /// Creates the banner view and loads a banner ad.
    /// </summary>
    public void LoadAd()
    {
        if (string.IsNullOrEmpty(_adUnitId))
            IniUnitId();

        if (!isActiveAndEnabled || !AdsInitializer.IsReady || PlayerAccountInfo.Me == null ||
            PlayerAccountInfo.Me.noAdsState || string.IsNullOrEmpty(_adUnitId))
            return;
        // create an instance of a banner view first.
        if(_bannerView == null)
        {
            CreateBannerView();
        }

        // create our request used to load the ad.
        var adRequest = new AdRequest();

        // send the request to load the ad.
        Debug.Log("Loading banner ad.");
        _bannerView.LoadAd(adRequest);
    }

    void Update()
    {
        // A no-ads purchase can complete while this screen is still open.
        if (_bannerView != null && PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState)
            DestroyBannerView();
        else if (_bannerView != null && (_requestedScreen.x != Screen.width || _requestedScreen.y != Screen.height
            || _requestedSafeArea != Screen.safeArea))
        {
            // Adaptive height is orientation-dependent. A replacement request
            // owns its own occupancy; callbacks from the old view are ignored.
            DestroyBannerView();
            LoadAd();
        }
    }

    /// <summary>
    /// listen to events the banner view may raise.
    /// </summary>
    private void ListenToAdEvents()
    {
        var bannerView = _bannerView;
        // Raised when an ad is loaded into the banner view.
        bannerView.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (this == null || !isActiveAndEnabled || _bannerView != bannerView) return;
            _hasLoaded = true;
            RefreshOccupiedArea();
            Debug.Log("Banner view loaded.");
        });
        // Raised when an ad fails to load into the banner view.
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (this == null || !isActiveAndEnabled || _bannerView != bannerView) return;
            // A failed refresh can leave the previous ad visible. Keep its
            // space, while an initial failure never reserves an empty row.
            RefreshOccupiedArea();
            Debug.LogWarning("Banner view failed to load an ad with error : " + error);
        });
        // Raised when the ad is estimated to have earned money.
        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Banner view paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        bannerView.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Banner view recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        bannerView.OnAdClicked += () =>
        {
            Debug.Log("Banner view was clicked.");
        };
        // Raised when an ad opened full screen content.
        bannerView.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Banner view full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        bannerView.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Banner view full screen content closed.");
        };
    }

    /// <summary>
    /// Destroys the banner view.
    /// </summary>
    public void DestroyBannerView()
    {
        _hasLoaded = false;
        SetOccupiedHeight(0);
        if (_bannerView != null)
        {
            Debug.Log("Destroying banner view.");
            _bannerView.Destroy();
            _bannerView = null;
        }
    }

    // Tuple<Vector2, Vector2> CalSize()
    // {
    //     var screenHeight = Screen.height * (posRef.rect.height / PosCal.CanvasHeight);
    //     var screenWidth = Screen.width * (posRef.rect.width / PosCal.CanvasWidth);
    //     var pos = new Vector2(0, Screen.safeArea.size.y + Screen.safeArea.position.y - screenHeight);
    //     //Debug.Log("pos :"+ pos);
    //     //Debug.Log("size :"+ new Vector2(screenWidth, screenHeight));
    //     return new Tuple<Vector2, Vector2>(new Vector2(screenWidth, screenHeight), Vector2.zero);
    // }

    // void OnGUI()
    // {
    //     var rect = CalSize();
    //     if (GUI.Button(new Rect(rect.Item2.x, rect.Item2.y,rect.Item1.x, rect.Item1.y), "I am a button"))
    //     {
    //         Debug.Log(rect.Item2.x + " : " + rect.Item2.y);
    //         Debug.Log(rect.Item1.x + " : " + rect.Item1.y);
    //         print("You clicked the button!");
    //     }
    // }
}

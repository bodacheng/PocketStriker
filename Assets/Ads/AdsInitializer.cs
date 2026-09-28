using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

public class AdsInitializer : MonoBehaviour
{
    public static AdsInitializer target;

    public static bool IsReady { get; private set; }
    public static bool PrivacyOptionsRequired =>
        ShouldEnableAds() && _consentUpdated &&
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    public static event Action AdsReady;
    public static event Action PrivacyOptionsAvailabilityChanged;

    private static bool _startupRequested;
    private static bool _sdkInitializationStarted;
    private static bool _consentUpdated;

    public bool Initialized => IsReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        target = null;
        IsReady = false;
        _startupRequested = false;
        _sdkInitializationStarted = false;
        _consentUpdated = false;
        AdsReady = null;
        PrivacyOptionsAvailabilityChanged = null;
    }

    private void Awake()
    {
        target = this;
        if (!ShouldEnableAds() || _startupRequested)
            return;

        _startupRequested = true;
        // AdMob can preload ads during initialization, so resolve consent first.
        ConsentInformation.Update(new ConsentRequestParameters(), OnConsentUpdated);
    }

    private void OnDestroy()
    {
        if (target == this)
            target = null;
    }

    public static bool ShouldEnableAds()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer ||
               Application.platform == RuntimePlatform.Android;
    }

    private static void OnConsentUpdated(FormError error)
    {
        _consentUpdated = true;
        MobileAdsEventExecutor.ExecuteInUpdate(() => PrivacyOptionsAvailabilityChanged?.Invoke());

        if (error != null)
        {
            Debug.LogWarning("AdMob consent update failed: " + error.Message);
            TryInitializeAds(); // UMP may retain a usable decision from a previous session.
            return;
        }

        ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
        {
            if (formError != null)
                Debug.LogWarning("AdMob consent form failed: " + formError.Message);

            MobileAdsEventExecutor.ExecuteInUpdate(() => PrivacyOptionsAvailabilityChanged?.Invoke());
            TryInitializeAds();
        });
    }

    private static void TryInitializeAds()
    {
        if (_sdkInitializationStarted || !ConsentInformation.CanRequestAds())
            return;

        _sdkInitializationStarted = true;
        // Ad callbacks that touch Unity objects marshal their work to the Unity thread.
        MobileAds.Initialize(status =>
        {
            if (status == null)
            {
                Debug.LogError("Google Mobile Ads initialization failed.");
                _sdkInitializationStarted = false;
                return;
            }

            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.Log("Google Mobile Ads initialized: " + status);
                IsReady = true;
                AdsReady?.Invoke();
            });
        });
    }

    public static void ShowPrivacyOptionsForm()
    {
        if (!PrivacyOptionsRequired)
            return;

        ConsentForm.ShowPrivacyOptionsForm(error =>
        {
            if (error != null)
                Debug.LogWarning("AdMob privacy options form failed: " + error.Message);
            MobileAdsEventExecutor.ExecuteInUpdate(() => PrivacyOptionsAvailabilityChanged?.Invoke());
            TryInitializeAds();
        });
    }
}

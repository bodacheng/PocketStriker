using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

// Consumer policy also applies to request options read from a cached catalog.
public static class PocketStrikerDownloadPolicy
{
    public const int MaxConcurrentRequests = 4;
    public const int RequestTimeoutSeconds = 30;
    public const int BundleRetryCount = 2;
    public const int DownloadAttempts = 2;
    // Small required updates finish during startup inspection without download UI.
    // The normal cache/retry path still runs. Larger downloads keep consent.
    public const long AutomaticDownloadLimitBytes = 64 * 1024;

    public static bool RequiresDownloadConfirmation(long requiredBytes) =>
        requiredBytes > AutomaticDownloadLimitBytes;

    static Func<IResourceLocation, string> previousTransform;
    static Action<UnityWebRequest> previousRequestOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Configure()
    {
        if (Addressables.InternalIdTransformFunc != TransformLocation)
        {
            previousTransform = Addressables.InternalIdTransformFunc;
            Addressables.InternalIdTransformFunc = TransformLocation;
        }
        if (Addressables.WebRequestOverride != ConfigureRequest)
        {
            previousRequestOverride = Addressables.WebRequestOverride;
            Addressables.WebRequestOverride = ConfigureRequest;
        }
        // Addressables initialization can restore the value in its built settings.
        WebRequestQueue.SetMaxConcurrentRequests(MaxConcurrentRequests);
    }

    static string TransformLocation(IResourceLocation location)
    {
        if (location.Data is AssetBundleRequestOptions options)
        {
            // AssetBundleProvider treats this as an idle timeout and resets it
            // whenever bytes arrive, so a slow large download is allowed to finish.
            options.Timeout = RequestTimeoutSeconds;
            options.RetryCount = BundleRetryCount;
        }
        return previousTransform == null ? location.InternalId : previousTransform(location);
    }

    static void ConfigureRequest(UnityWebRequest request)
    {
        previousRequestOverride?.Invoke(request);
        if (!Uri.TryCreate(request.url, UriKind.Absolute, out var uri)) return;
        var fileName = Path.GetFileName(uri.AbsolutePath);
        // Catalog/hash requests need a bound too. Do not set UnityWebRequest's
        // whole-transfer timeout on AssetBundles; they use the idle timeout above.
        if (fileName.StartsWith("catalog", StringComparison.OrdinalIgnoreCase) &&
            (fileName.EndsWith(".hash", StringComparison.OrdinalIgnoreCase) ||
             fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
             fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            request.timeout = RequestTimeoutSeconds;
    }
}

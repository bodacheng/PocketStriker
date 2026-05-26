using System;
using System.Collections.Generic;
using UnityEngine;

public static class AddressablesResourcePolicy
{
    public const string AppVersionKey = "app_version";
    public const string VersionJsonProperty = "version";
    public const string ConfigLabel = "config";
    public const string CommonSettingKey = "Config/commonSetting";

    public const string UnitLabel = "unit";
    public const string BasicAnimationLabel = "basic_anim";
    public const string SkillAnimationLabel = "skill_anim";
    public const string SkillIconLabel = "skill_icon";
    public const string WeaponLabel = "weapon";
    public const string EffectLabel = "effect";
    public const string AudioLabel = "audio";
    public const string UnitImageLabel = "unit_image";

    public static readonly string[] MCombatEssentialLabels =
    {
        WeaponLabel,
        EffectLabel,
        AudioLabel,
        UnitImageLabel
    };

    public static readonly string[] FullCombatEssentialLabels =
    {
        UnitLabel,
        SkillAnimationLabel,
        SkillIconLabel,
        WeaponLabel,
        EffectLabel,
        AudioLabel,
        UnitImageLabel
    };

    public static bool IsNonCriticalAssetType<T>()
    {
        var type = typeof(T);
        return type == typeof(Sprite)
               || type == typeof(AnimationClip)
               || type == typeof(AudioClip);
    }

    public static bool ShouldReturnToStartOnLoadFailure<T>()
    {
        return !IsNonCriticalAssetType<T>();
    }

    public static bool IsServerVersionNewer(string currentVersion, string serverVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(serverVersion))
        {
            return false;
        }

        var serverVersionParts = serverVersion.Split('.');
        var currentVersionParts = currentVersion.Split('.');
        var count = Math.Min(serverVersionParts.Length, currentVersionParts.Length);

        for (var i = 0; i < count; i++)
        {
            var serverVersionPart = ParseVersionPart(serverVersionParts[i]);
            var currentVersionPart = ParseVersionPart(currentVersionParts[i]);

            if (serverVersionPart > currentVersionPart)
            {
                return true;
            }

            if (serverVersionPart < currentVersionPart)
            {
                return false;
            }
        }

        return false;
    }

    public static string DownloadProgressText(SystemLanguage language)
    {
        return language switch
        {
            SystemLanguage.English => "Downloading Assets",
            SystemLanguage.Japanese => "リソースをダウンロード中です",
            SystemLanguage.Chinese => "正在下载资源",
            _ => "リソースをダウンロード中です"
        };
    }

    public static void EnsureDownloadedBytesLabel(IDictionary<string, long> downloadedBytes, string label)
    {
        if (downloadedBytes == null || string.IsNullOrEmpty(label) || downloadedBytes.ContainsKey(label))
        {
            return;
        }

        downloadedBytes.Add(label, 0);
    }

    public static string LoadFailureMessage(string key)
    {
        return $"[Addressables] Failed to load: {key}";
    }

    public static string ExceptionMessage(string key, Exception exception)
    {
        return $"[Addressables] Exception loading {key}: {exception?.Message}";
    }

    public static string DownloadSizeFailureMessage(string label)
    {
        return $"[Addressables] Failed to get download size for label: {label}";
    }

    public static string DownloadSizeExceptionMessage(string label, Exception exception)
    {
        return $"[Addressables] Exception during GetDownloadSizeAsync for label: {label}, Exception: {exception}";
    }

    public static string DownloadMissionExceptionMessage(Exception exception)
    {
        return $"DownloadMission Exception: {exception}";
    }

    public static string DownloadMissionFailureMessage(string label, Exception exception)
    {
        return $"[Addressables] DownloadDependenciesAsync failed for label: {label}, Exception: {exception}";
    }

    public static string InstantiateFailureMessage(string key)
    {
        return $"Failed to load : {key}";
    }

    private static int ParseVersionPart(string value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }
}

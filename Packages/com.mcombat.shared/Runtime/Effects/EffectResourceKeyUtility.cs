using System.Collections.Generic;

public static class EffectResourceKeyUtility
{
    public const string EffectLabel = "effect";
    public const string WeaponLabel = "weapon";
    public const string DefaultHitBoxPrefabName = "dHitBox";

    public static string ResourceKey(string resourcePath, string resourceName)
    {
        return Join(resourcePath, resourceName);
    }

    public static string PrefabAddress(string resourcePath, string resourceName)
    {
        return ResourceKey(resourcePath, resourceName) + ".prefab";
    }

    public static string DefaultHitBoxAddress(string defaultEffectPath)
    {
        return PrefabAddress(defaultEffectPath, DefaultHitBoxPrefabName);
    }

    public static IEnumerable<string> ResourcePathFallbacks(string preferredPath, string defaultPath)
    {
        if (!string.IsNullOrEmpty(preferredPath))
            yield return NormalizePath(preferredPath);

        var normalizedDefaultPath = NormalizePath(defaultPath);
        if (!string.IsNullOrEmpty(normalizedDefaultPath) && normalizedDefaultPath != NormalizePath(preferredPath))
            yield return normalizedDefaultPath;
    }

    private static string Join(params string[] parts)
    {
        return NormalizePath(string.Join("/", parts));
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\\', '/').Trim('/');
    }
}

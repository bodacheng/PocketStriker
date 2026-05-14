using System;

public static class AnimationResourceKeyUtility
{
    public const string BasicAnimationLabel = AddressablesResourcePolicy.BasicAnimationLabel;
    public const string SkillFolderName = "skill";
    public const string BasicPackFolderName = "BasicPack";

    public static string BasicPackSeriesKey(string type, string basicPackName)
    {
        return Join(type, basicPackName);
    }

    public static string SeriesKey(string type, string address)
    {
        return Join(type, address);
    }

    public static string SkillClipKey(string type, string skillKey)
    {
        return Join(type, SkillFolderName, skillKey);
    }

    public static string SkillAnimationAddress(string type, string skillKey)
    {
        return SkillClipKey(type, skillKey) + ".anim";
    }

    public static bool IsBasicAnimationLocation(string primaryKey, string type, string basicPackName)
    {
        var key = NormalizePath(primaryKey);
        var typePath = NormalizePath(type);
        var packPath = NormalizePath(basicPackName);
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(typePath) || string.IsNullOrEmpty(packPath))
        {
            return false;
        }

        return IsLocationUnder(key, Join(typePath, packPath)) ||
               IsLocationUnder(key, Join(typePath, BasicPackFolderName, packPath));
    }

    public static bool IsSeriesAnimationLocation(string primaryKey, string seriesKey)
    {
        return IsLocationUnder(NormalizePath(primaryKey), NormalizePath(seriesKey));
    }

    private static bool IsLocationUnder(string key, string prefix)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        return string.Equals(key, prefix, StringComparison.Ordinal) ||
               key.StartsWith(prefix + "/", StringComparison.Ordinal);
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

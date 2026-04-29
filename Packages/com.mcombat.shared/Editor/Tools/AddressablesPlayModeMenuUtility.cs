using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;

public static class AddressablesPlayModeMenuUtility
{
    private static readonly IReadOnlyDictionary<int, string> DefaultMenuPaths = new Dictionary<int, string>
    {
        { 0, "MCombat/AddressableAssets/PlayMode/Use Local" },
        { 2, "MCombat/AddressableAssets/PlayMode/Use Remote" }
    };

    public static void RefreshMenuChecks()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return;
        }

        RefreshMenuChecks(settings.ActivePlayModeDataBuilderIndex);
    }

    public static void SetActivePlayMode(int mode)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return;
        }

        settings.ActivePlayModeDataBuilderIndex = mode;
        RefreshMenuChecks(mode);
    }

    private static void RefreshMenuChecks(int activeMode)
    {
        foreach (var kv in DefaultMenuPaths)
        {
            Menu.SetChecked(kv.Value, activeMode == kv.Key);
        }
    }
}

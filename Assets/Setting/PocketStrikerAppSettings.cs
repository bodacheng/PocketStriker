using UnityEngine;

/// <summary>Project defaults for the shared, platform-neutral settings model.</summary>
public static class PocketStrikerAppSettings
{
    public static void Load()
    {
        AppSetting.Load();
        var settings = AppSetting.Value;
        if (settings.UpKeyCode == KeyCode.None) settings.UpKeyCode = KeyCode.W;
        if (settings.DownKeyCode == KeyCode.None) settings.DownKeyCode = KeyCode.S;
        if (settings.LeftKeyCode == KeyCode.None) settings.LeftKeyCode = KeyCode.A;
        if (settings.RightKeyCode == KeyCode.None) settings.RightKeyCode = KeyCode.D;
    }
}

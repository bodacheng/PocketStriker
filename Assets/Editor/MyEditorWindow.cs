using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public static class MyEditorWindow
{
    readonly static Dictionary<int,string> menuPath = new Dictionary<int,string>(){
        {0, "P3/AddressableAssets/PlayMode/Use Local"},
        {2, "P3/AddressableAssets/PlayMode/Use Remote"}
    };
    
    [MenuItem("MCombat/StageManager", priority = 1)]
    static void StageManager()
    {
        var window = (StageEditorWindow)EditorWindow.GetWindow(typeof(StageEditorWindow));
        window.titleContent = new GUIContent("关卡管理器");
        window.Show();
    }

    [MenuItem("MCombat/MasterDataTool", priority = 2)]
    static void MasterDataTool()
    {
        LocalMasterDataToolGUI window = (LocalMasterDataToolGUI)EditorWindow.GetWindow(typeof(LocalMasterDataToolGUI));
        window.titleContent = new GUIContent("Master Data 出力工具");
        window.Show();
    }
    
    [MenuItem("MCombat/Skill Analise", priority = 3)]
    static void SKillAnalyzer()
    {
        SKillAnalyzerGUI window = (SKillAnalyzerGUI)EditorWindow.GetWindow(typeof(SKillAnalyzerGUI));
        window.titleContent = new GUIContent("技能分析工具");
        window.Show();
    }
    
    [MenuItem("MCombat/other tool", priority = 4)]
    static void OtherTool()
    {
        OtherTool window = (OtherTool)EditorWindow.GetWindow(typeof(OtherTool));
        window.titleContent = new GUIContent("其他工具");
        window.Show();
    }
    
    static MyEditorWindow()
    {
        EditorApplication.delayCall += () => ChangePlayMode(AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex);
    }
    
    [MenuItem("MCombat/AddressableAssets/PlayMode/Use Local")]
    static void PlayModeUseLocal() => ChangePlayMode(0);
    [MenuItem("MCombat/AddressableAssets/PlayMode/Use Remote")]
    static void PlayModeUseRemote() => ChangePlayMode(2);
    
    static void ChangePlayMode(int mode)
    {
        foreach ( var kv in menuPath)
        {
            Menu.SetChecked(kv.Value, mode==kv.Key);
        }

        AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = mode;
    }
}
#endif

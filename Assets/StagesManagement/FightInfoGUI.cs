#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(FightInfo))]
public class FightInfoGUI : Editor
{
    private StageEditor _stageEditor;
    private bool _initialized = false;
    
    public override void OnInspectorGUI()
    {
        if (!Starter.ConfigInitialised)
        {
            EditorGUILayout.LabelField("Loading config");
        }
        
        DrawDefaultInspector();
        var fightInfo = (FightInfo)target;
        if (!_initialized)
        {
            fightInfo.OpenAndSetEnemyDataOnPlace();
            _stageEditor = new StageEditor();
            _initialized = true;
        }
        
        fightInfo.EvolutionMode = EditorGUILayout.Toggle("进化模式", fightInfo.EvolutionMode);
        if (fightInfo.EvolutionMode)
        {
            if (GUILayout.Button("进化模式随机全部队员"))
            {
                fightInfo.FightMembers = new FightMembers();
                fightInfo.UnitsData = new List<UnitInfo>();
                fightInfo.EvolutionMode = true;
                SaveProcess();
                return;
            }
        }
        
        fightInfo.SetUnitLevelByRefLevel();
        _stageEditor.OnGUIView(fightInfo.FightMembers,null, ()=>
        {
            if (GUILayout.Button("Save"))
            {
                SaveProcess();
            }
        });

        void SaveProcess()
        {
            fightInfo.SaveDicToData();
            EditorUtility.SetDirty(fightInfo);
            AssetDatabase.SaveAssets();
        }
    }
    
    public static Sprite GetSprite(string name)
    {
        var searchRootAssetFolder = Application.dataPath;
        var pfGuiPaths = Directory.GetFiles(searchRootAssetFolder, name, SearchOption.AllDirectories);
        foreach (var eachPath in pfGuiPaths)
        {
            var loadPath = eachPath.Substring(eachPath.LastIndexOf("Assets"));
            var sprite =(Sprite)AssetDatabase.LoadAssetAtPath(loadPath, typeof(Sprite));
            return sprite;
        }
        return null;
    }
}
#endif
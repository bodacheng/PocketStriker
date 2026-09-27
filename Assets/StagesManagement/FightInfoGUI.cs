#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(FightInfo))]
public class FightInfoGUI : Editor
{
    private StageEditor _stageEditor;

    void OnDisable()
    {
        _stageEditor?.Dispose();
        _stageEditor = null;
        _initialized = false;
    }

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
    
    static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    static FightInfoGUI()
    {
        EditorApplication.projectChanged += SpriteCache.Clear;
    }

    public static Sprite GetSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (SpriteCache.TryGetValue(name, out var cached)) return cached;
        Sprite sprite = null;
        if (name.StartsWith("Assets/", StringComparison.Ordinal))
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(name);
        else
        {
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(name) + " t:Sprite", new[] { "Assets" }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileName(assetPath), name, StringComparison.OrdinalIgnoreCase)) continue;
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null) break;
            }
        }
        SpriteCache[name] = sprite;
        return sprite;
    }
}
#endif

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(GangbangInfo))]
public class GangbangInfoGUI : Editor
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
        var gangbangInfo = (GangbangInfo)target;
        if (!_initialized)
        {
            gangbangInfo.OpenAndSetEnemyDataOnPlace();
            _stageEditor = new StageEditor();
            _initialized = true;
        }
        _stageEditor.OnGUIView(gangbangInfo.FightMembers, gangbangInfo.GetTeam2GroupSet);
        
        if (GUILayout.Button("Save"))
        {
            gangbangInfo.SaveDicToData();
            EditorUtility.SetDirty(gangbangInfo);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
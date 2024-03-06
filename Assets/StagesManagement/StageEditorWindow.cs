#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

public class StageEditorWindow : EditorWindow
{
    private StageEditor _stageEditor;
    private FightInfo _target;
    string _pathAndNameForLocalSave = "Assets/ExternalAssets/ArcadeStages";
    string _fileName;
    bool _initialized;
    void OnGUI()
    {
        if (!Starter.ConfigInitialised)
        {
            EditorGUILayout.LabelField("Loading config");
        }
        
        if (!_initialized)
        {
            _target = new FightInfo();
            _target.FightMembers = new FightMembers();
            _stageEditor = new StageEditor();
            _initialized = true;
        }

        if (_target == null || _target.FightMembers == null || _stageEditor == null)
        {
            _initialized = false;
            return;
        }
        
        _stageEditor.OnGUIView(_target.FightMembers);
        
        _pathAndNameForLocalSave = EditorGUILayout.TextField("local Path For Saving", _pathAndNameForLocalSave);
        _fileName = EditorGUILayout.TextField("file", _fileName);
        
        if (GUILayout.Button("Save FightInfo"))
        {
            FightInfo.CreateFightInfoAsset(_target.FightMembers, _pathAndNameForLocalSave, _fileName);
        }
        
        if (GUILayout.Button("Save GangbangInfo"))
        {
            FightInfo.CreateGangbangInfoAsset(_target.FightMembers, _pathAndNameForLocalSave, _fileName);
        }
    }
}
#endif
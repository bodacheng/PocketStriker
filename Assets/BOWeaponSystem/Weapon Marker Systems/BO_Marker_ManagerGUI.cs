#if UNITY_EDITOR
using UnityEditor;
using HittingDetection;

[CustomEditor(typeof(HitBoxManager))]
public class BO_Marker_ManagerGUI : Editor
{        
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}

#endif
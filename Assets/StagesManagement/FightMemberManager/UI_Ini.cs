#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class StageEditor{

    bool Initialized = false;
    GUIStyle _buttonStyle;
    GUIStyle _unitIconStyle, _unitIconSelectedStyle;
    GUIContent _unitBtnContent;
    GUIStyle _addDeleteMember;
    GUIStyle _buttonStyleNineAndTwo;
    GUIStyle _buttonStyleNineAndTwoSelected;
    GUIStyle _title;
    GUIStyle _attackRangeToggleGUI;
    
    void UIParamIni()
    {
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.red
            },
            fixedWidth = 100f,
            alignment = TextAnchor.MiddleCenter
        };

        _unitIconStyle = new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.white
            },
            fixedWidth = 30f,
            fixedHeight = 30f,
            alignment = TextAnchor.MiddleCenter
        };
        
        _unitIconSelectedStyle= new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.yellow
            },
            fixedWidth = 40f,
            fixedHeight = 40f,
            alignment = TextAnchor.MiddleCenter
        };

        _addDeleteMember = new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = new Color(1, 0.3f, 0f)
            },
            fixedWidth = 50f,
            alignment = TextAnchor.MiddleCenter
        };

        _title = new GUIStyle(GUI.skin.label)
        {
            normal =
            {
                textColor = Color.blue
            },
            alignment = TextAnchor.MiddleCenter
        };
        
        _buttonStyleNineAndTwo = new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.blue
            },
            fixedWidth = 80f,
            alignment = TextAnchor.MiddleCenter
        };

        _buttonStyleNineAndTwoSelected = new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.yellow
            },
            fixedWidth = 80f,
            alignment = TextAnchor.MiddleCenter
        };

        _attackRangeToggleGUI = new GUIStyle(GUI.skin.toggle)
        {
            margin = new RectOffset(1, 1, 11, 11),
            alignment = TextAnchor.MiddleCenter,
            stretchWidth = false
        };
    }
}
#endif
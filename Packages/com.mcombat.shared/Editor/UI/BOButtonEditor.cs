using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(BOButton), true)]
[CanEditMultipleObjects]
public class P2ButtonEditor : ButtonEditor
{
    SerializedProperty disableSpriteProperty;
    SerializedProperty disableTransitionOverrideProperty;
    SerializedProperty activateDoubleClickProperty;
    SerializedProperty activateHoldProperty;
    SerializedProperty onDoubleClickProperty;
    SerializedProperty onHoldProperty;
    SerializedProperty onHoldRepeatProperty;
    SerializedProperty textProperty;
    SerializedProperty soundProperty;

    protected override void OnEnable()
    {
        base.OnEnable();
        disableSpriteProperty = serializedObject.FindProperty("disableSprite");
        disableTransitionOverrideProperty = serializedObject.FindProperty("disableTransitionOverride");
        activateDoubleClickProperty = serializedObject.FindProperty("activateDoubleClick");
        activateHoldProperty = serializedObject.FindProperty("activateHold");
        onDoubleClickProperty = serializedObject.FindProperty("doubleClick");
        onHoldProperty = serializedObject.FindProperty("hold");
        onHoldRepeatProperty = serializedObject.FindProperty("repeatHold");
        textProperty = serializedObject.FindProperty("text");
        soundProperty = serializedObject.FindProperty("sound");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(disableTransitionOverrideProperty);
        EditorGUILayout.PropertyField(disableSpriteProperty);
        EditorGUILayout.PropertyField(disableSpriteProperty);
        EditorGUILayout.PropertyField(textProperty);
        serializedObject.ApplyModifiedProperties();

        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.PropertyField(activateDoubleClickProperty);
        if (activateDoubleClickProperty.boolValue)
            EditorGUILayout.PropertyField(onDoubleClickProperty);

        EditorGUILayout.PropertyField(activateHoldProperty);
        EditorGUILayout.PropertyField(soundProperty);
        if (activateHoldProperty.boolValue)
        {
            EditorGUILayout.PropertyField(onHoldProperty);
            EditorGUILayout.PropertyField(onHoldRepeatProperty);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MCombat.Shared.Behaviour;
using Soul;
using Skill;

[CustomEditor(typeof(BehaviorRunner))]
public class BehaviorRunnerGUI : Editor
{
    static readonly string[] DefaultStateNames = { "Death", "Empty", "Hit", "KnockOff", "Move", "Victory", "getUp", "rush" };
    static readonly int[] SpLevelValues = { 0, 1, 2, 3 };
    static readonly string[] SpLevelLabels = { "normal", "ex1", "ex2", "ex3" };
    static readonly int[] HeightValues = { 0, 1, 2 };
    static readonly string[] HeightLabels = { "Low", "Mid", "High" };

    BehaviorRunner runner;
    SerializedProperty skillListProp;
    SerializedProperty skillConfigTypeProp;
    SerializedProperty aiStatesPathProp;
    SerializedProperty usingScriptProp;

    readonly List<bool> stateFoldouts = new List<bool>();
    readonly List<bool> casualFoldouts = new List<bool>();
    readonly List<bool> forcedFoldouts = new List<bool>();

    bool useLocalResourceReference;
    Vector2 scrollPosition;

    void OnEnable()
    {
        runner = (BehaviorRunner)target;
        EnsureSkillList();

        serializedObject.Update();
        CacheSerializedProperties();
        SyncFoldoutCaches(skillListProp?.arraySize ?? 0);
        serializedObject.ApplyModifiedProperties();
    }

    void EnsureSkillList()
    {
        if (runner != null && runner.skillEntityList == null)
        {
            runner.skillEntityList = new List<SkillEntity>();
            EditorUtility.SetDirty(runner);
        }
    }

    void CacheSerializedProperties()
    {
        skillListProp = serializedObject.FindProperty("skillEntityList");
        skillConfigTypeProp = serializedObject.FindProperty("skillConfigType");
        aiStatesPathProp = serializedObject.FindProperty("AI_States_path");
        usingScriptProp = serializedObject.FindProperty("usingScript");
    }

    void SyncFoldoutCaches(int count)
    {
        SyncFoldoutList(stateFoldouts, count, true);
        SyncFoldoutList(casualFoldouts, count, false);
        SyncFoldoutList(forcedFoldouts, count, false);
    }

    static void SyncFoldoutList(List<bool> list, int targetCount, bool defaultValue)
    {
        while (list.Count < targetCount) list.Add(defaultValue);
        if (list.Count > targetCount) list.RemoveRange(targetCount, list.Count - targetCount);
    }

    public override void OnInspectorGUI()
    {
        runner = (BehaviorRunner)target;
        EnsureSkillList();

        serializedObject.Update();
        CacheSerializedProperties();
        SyncFoldoutCaches(skillListProp?.arraySize ?? 0);

        DrawHeader();

        EditorGUILayout.Space();

        var stateOptions = BuildStateOptions();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawSkillEntityList(stateOptions);
        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawHeader()
    {
        EditorGUILayout.LabelField("Behavior Runner", EditorStyles.boldLabel);

        useLocalResourceReference = EditorGUILayout.Toggle("本地资源参照模式", useLocalResourceReference);

        if (skillConfigTypeProp != null)
        {
            EditorGUILayout.PropertyField(skillConfigTypeProp, new GUIContent("技能类型标识"));
        }

        if (aiStatesPathProp != null)
        {
            EditorGUILayout.PropertyField(aiStatesPathProp, new GUIContent("AI States Path"));
        }

        if (usingScriptProp != null)
        {
            EditorGUILayout.PropertyField(usingScriptProp, new GUIContent("Using Script"));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(skillConfigTypeProp?.stringValue)))
            {
                if (GUILayout.Button("保存状态迁移"))
                {
                    serializedObject.ApplyModifiedProperties();

                    if (string.IsNullOrEmpty(runner.SkillConfigType))
                    {
                        Debug.LogWarning("SkillConfigType 未设置，无法保存状态迁移。");
                    }
                    else
                    {
                        runner.SaveTrans(runner.SkillConfigType);
                        EditorUtility.SetDirty(runner);
                    }

                    serializedObject.Update();
                }
            }
        }

        if (useLocalResourceReference && string.IsNullOrEmpty(skillConfigTypeProp?.stringValue))
        {
            EditorGUILayout.HelpBox("启用本地资源参照模式时需要指定技能类型标识，用于从技能配置表加载状态列表。", MessageType.Info);
        }
    }

    List<string> BuildStateOptions()
    {
        var options = new HashSet<string>(DefaultStateNames);

        if (!useLocalResourceReference)
        {
            if (skillListProp != null)
            {
                for (var i = 0; i < skillListProp.arraySize; i++)
                {
                    var element = skillListProp.GetArrayElementAtIndex(i);
                    var realNameProp = element.FindPropertyRelative("REAL_NAME");
                    if (!string.IsNullOrEmpty(realNameProp.stringValue))
                    {
                        options.Add(realNameProp.stringValue);
                    }
                }
            }
        }
        else if (!string.IsNullOrEmpty(skillConfigTypeProp?.stringValue))
        {
            var fromConfig = GetBeheviourOptions(skillConfigTypeProp.stringValue);
            if (fromConfig != null)
            {
                foreach (var name in fromConfig)
                {
                    if (!string.IsNullOrEmpty(name))
                        options.Add(name);
                }
            }
        }

        return options.OrderBy(x => x).ToList();
    }

    void DrawSkillEntityList(IReadOnlyList<string> stateOptions)
    {
        if (skillListProp == null)
        {
            EditorGUILayout.HelpBox("skillEntityList 未初始化。", MessageType.Error);
            return;
        }

        for (var i = 0; i < skillListProp.arraySize; i++)
        {
            var skillProp = skillListProp.GetArrayElementAtIndex(i);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSkillHeader(skillProp, i);
                if (!stateFoldouts[i])
                {
                    continue;
                }

                EditorGUI.indentLevel++;

                DrawSkillBasics(skillProp, stateOptions);
                EditorGUILayout.Space();
                DrawAIAttributes(skillProp);
                EditorGUILayout.Space();
                DrawCasualTransitions(skillProp, i, stateOptions);
                EditorGUILayout.Space();
                DrawForcedTransitions(skillProp, i);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
        }

        if (GUILayout.Button("添加状态"))
        {
            AddNewSkillEntity();
        }
    }

    void DrawSkillHeader(SerializedProperty skillProp, int index)
    {
        var realNameProp = skillProp.FindPropertyRelative("REAL_NAME");
        var displayName = string.IsNullOrEmpty(realNameProp.stringValue)
            ? $"State {index + 1}"
            : realNameProp.stringValue;

        using (new EditorGUILayout.HorizontalScope())
        {
            stateFoldouts[index] = EditorGUILayout.Foldout(stateFoldouts[index], displayName, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("删除", GUILayout.Width(56f)))
            {
                RemoveSkillEntityAt(index);
                GUIUtility.ExitGUI();
            }
        }
    }

    void DrawSkillBasics(SerializedProperty skillProp, IReadOnlyList<string> stateOptions)
    {
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("SkillID"), new GUIContent("Skill ID"));
        DrawStateKeyField(skillProp.FindPropertyRelative("REAL_NAME"), stateOptions);
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("StateType"), new GUIContent("State Type"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("CAN_BE_CANCELLED_TO"), new GUIContent("Super Cancel"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("EnterInput"), new GUIContent("Enter Input"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("ExitInput"), new GUIContent("Exit Input"));

        var spLevelProp = skillProp.FindPropertyRelative("SP_LEVEL");
        var spSelected = EditorGUILayout.IntPopup("SP Level", spLevelProp.intValue, SpLevelLabels, SpLevelValues);
        if (spSelected != spLevelProp.intValue)
        {
            spLevelProp.intValue = spSelected;
        }
    }

    void DrawAIAttributes(SerializedProperty skillProp)
    {
        var aiAttrsProp = skillProp.FindPropertyRelative("AIAttrs");
        if (aiAttrsProp == null)
        {
            EditorGUILayout.HelpBox("AIAttrs 数据为空。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("AI 属性", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        var minProp = aiAttrsProp.FindPropertyRelative("AI_MIN_DIS");
        var maxProp = aiAttrsProp.FindPropertyRelative("AI_MAX_DIS");
        var heightProp = aiAttrsProp.FindPropertyRelative("height");

        if (minProp != null)
        {
            EditorGUILayout.PropertyField(minProp, new GUIContent("Distance Min"));
        }
        if (maxProp != null)
        {
            EditorGUILayout.PropertyField(maxProp, new GUIContent("Distance Max"));
        }
        if (heightProp != null)
        {
            var selectedHeight = EditorGUILayout.IntPopup("Trigger Height", heightProp.intValue, HeightLabels, HeightValues);
            if (selectedHeight != heightProp.intValue)
            {
                heightProp.intValue = selectedHeight;
            }
        }
        EditorGUI.indentLevel--;
    }

    void DrawCasualTransitions(SerializedProperty skillProp, int index, IReadOnlyList<string> stateOptions)
    {
        var casualProp = skillProp.FindPropertyRelative("CasualTo");
        if (casualProp == null)
        {
            EditorGUILayout.HelpBox("CasualTo 数据为空。", MessageType.Warning);
            return;
        }

        casualFoldouts[index] = EditorGUILayout.Foldout(casualFoldouts[index], "自然迁移", true);
        if (!casualFoldouts[index])
        {
            return;
        }

        EditorGUI.indentLevel++;
        for (var i = 0; i < casualProp.arraySize; i++)
        {
            var element = casualProp.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStateKeyField(element, stateOptions, $"迁移 {i + 1}");
                if (GUILayout.Button("删除", GUILayout.Width(56f)))
                {
                    casualProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }

        if (GUILayout.Button("添加自然迁移"))
        {
            AddTransition(casualProp, stateOptions, skillProp.FindPropertyRelative("REAL_NAME")?.stringValue);
        }
        EditorGUI.indentLevel--;
    }

    void DrawForcedTransitions(SerializedProperty skillProp, int index)
    {
        var forcedProp = skillProp.FindPropertyRelative("ForcedTransitions");
        if (forcedProp == null)
        {
            EditorGUILayout.HelpBox("ForcedTransitions 数据为空。", MessageType.Warning);
            return;
        }

        forcedFoldouts[index] = EditorGUILayout.Foldout(forcedFoldouts[index], "强制迁移", true);
        if (!forcedFoldouts[index])
        {
            return;
        }

        EditorGUI.indentLevel++;
        for (var i = 0; i < forcedProp.arraySize; i++)
        {
            var element = forcedProp.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                element.stringValue = EditorGUILayout.DelayedTextField($"目标 {i + 1}", element.stringValue);
                if (GUILayout.Button("删除", GUILayout.Width(56f)))
                {
                    forcedProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }

        if (GUILayout.Button("添加强制迁移"))
        {
            var newIndex = forcedProp.arraySize;
            forcedProp.arraySize++;
            forcedProp.GetArrayElementAtIndex(newIndex).stringValue = string.Empty;
        }
        EditorGUI.indentLevel--;
    }

    void DrawStateKeyField(SerializedProperty stringProp, IReadOnlyList<string> stateOptions, string labelOverride = null)
    {
        if (stringProp == null)
        {
            return;
        }

        var current = stringProp.stringValue;
        var popupOptions = new List<string> { "<保持当前>" };

        if (!string.IsNullOrEmpty(current) && !popupOptions.Contains(current))
        {
            popupOptions.Add(current);
        }

        foreach (var option in stateOptions)
        {
            if (!popupOptions.Contains(option))
            {
                popupOptions.Add(option);
            }
        }

        var currentIndex = popupOptions.IndexOf(current);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var selected = EditorGUILayout.Popup(string.IsNullOrEmpty(labelOverride) ? "State Key" : labelOverride, currentIndex, popupOptions.ToArray());
        if (selected > 0 && selected < popupOptions.Count)
        {
            current = popupOptions[selected];
        }

        current = EditorGUILayout.DelayedTextField("State Key(手动)", current);
        stringProp.stringValue = current;
    }

    void AddTransition(SerializedProperty arrayProp, IReadOnlyList<string> stateOptions, string ownerState)
    {
        var candidate = stateOptions.FirstOrDefault(option =>
            !string.IsNullOrEmpty(option) &&
            option != ownerState &&
            !PropertyContains(arrayProp, option));

        var newIndex = arrayProp.arraySize;
        arrayProp.arraySize++;
        arrayProp.GetArrayElementAtIndex(newIndex).stringValue = candidate ?? string.Empty;
    }

    static bool PropertyContains(SerializedProperty arrayProp, string value)
    {
        for (var i = 0; i < arrayProp.arraySize; i++)
        {
            if (arrayProp.GetArrayElementAtIndex(i).stringValue == value)
            {
                return true;
            }
        }
        return false;
    }

    void AddNewSkillEntity()
    {
        serializedObject.ApplyModifiedProperties();

        Undo.RecordObject(runner, "Add Behavior State");
        runner.skillEntityList.Add(new SkillEntity
        {
            SkillID = string.Empty,
            REAL_NAME = string.Empty,
            StateType = BehaviorType.NONE,
            AIAttrs = new AIAttrs(),
            CasualTo = Array.Empty<string>(),
            ForcedTransitions = Array.Empty<string>(),
            CAN_BE_CANCELLED_TO = true,
            EnterInput = InputKey.Null,
            ExitInput = InputKey.Null,
            SP_LEVEL = 0
        });
        EditorUtility.SetDirty(runner);

        serializedObject.Update();
        CacheSerializedProperties();
        SyncFoldoutCaches(skillListProp?.arraySize ?? 0);
    }

    void RemoveSkillEntityAt(int index)
    {
        serializedObject.ApplyModifiedProperties();

        if (runner.skillEntityList == null || index < 0 || index >= runner.skillEntityList.Count)
        {
            return;
        }

        Undo.RecordObject(runner, "Remove Behavior State");
        runner.skillEntityList.RemoveAt(index);
        EditorUtility.SetDirty(runner);

        serializedObject.Update();
        CacheSerializedProperties();
        SyncFoldoutCaches(skillListProp?.arraySize ?? 0);
    }

    public List<string> GetBeheviourOptions(string anim_path)
    {
        if (anim_path == null)
        {
            return null;
        }

        List<SkillConfig> SkillConfigs = SkillConfigTable.GetSkillConfigsOfType(anim_path);
        List<string> returnValue = BehaviorStateDefinitionUtility.CreateEditorBehaviorOptions(FightGlobalSetting.HasDefend, true);

        foreach (SkillConfig skillConfig in SkillConfigs)
        {
            if (!returnValue.Contains(skillConfig.REAL_NAME))
                returnValue.Add(skillConfig.REAL_NAME);
            else
                Debug.Log("重复的片段名，请检查资源");
        }
        return returnValue;
    }

    public List<string> GetBeheviourOptions(string anim_path, List<SkillEntity> toFormAttackStateList)
    {
        if (anim_path == null)
        {
            return null;
        }

        List<string> returnValue = new List<string>();
        foreach (SkillEntity _set in toFormAttackStateList)
        {
            if (!returnValue.Contains(_set.REAL_NAME))
            {
                returnValue.Add(_set.REAL_NAME);
            }
            else
            {
                Debug.Log("正在回避状态重复定义："+ _set.REAL_NAME);
            }
        }

        return returnValue;
    }
}
#endif

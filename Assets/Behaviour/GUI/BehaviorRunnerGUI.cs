#if UNITY_EDITOR
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Soul;
using Skill;

[CustomEditor(typeof(BehaviorRunner))]
public partial class BehaviorRunnerGUI : Editor {

    BehaviorRunner myScript;

    bool GUIIniDone;
    GUIStyle ButtonStyle;
    GUIStyle addCasualToButtonStyle,deleteCasualToButtonStyle;
    GUIStyle stateKeyGUI;
    GUIStyle attackRangeToggleGUI;

    
    string[] StateIndexListOptions;
    string[] casualToStateKeyOptions;

    string targetType;
    List<string> casualToStateKeyOptionsList;
    
    bool LocalResourceReferenceMode;
    readonly int[] exoptions = { 0, 1, 2, 3 };
    readonly string[] exoptions_display = {"normal","ex1","ex2","ex3"};

    public override void OnInspectorGUI()
    {
        if (GUIIniDone)
        {
            ButtonStyle = new GUIStyle(GUI.skin.button);
            ButtonStyle.normal.textColor = Color.white;
            ButtonStyle.fixedWidth = 100f;
            
            addCasualToButtonStyle = new GUIStyle(GUI.skin.box);
            addCasualToButtonStyle.normal.textColor = Color.red;
            addCasualToButtonStyle.alignment = TextAnchor.MiddleCenter;
            addCasualToButtonStyle.margin = new RectOffset(100, 22, 11, 11);
            
            deleteCasualToButtonStyle = new GUIStyle(GUI.skin.box);
            deleteCasualToButtonStyle.normal.textColor = Color.blue;
            deleteCasualToButtonStyle.alignment = TextAnchor.MiddleCenter;
            deleteCasualToButtonStyle.margin = new RectOffset(50, 22, 11, 11);
            
            GUIIniDone = true;
        }
      
        LocalResourceReferenceMode = EditorGUILayout.Toggle("本地资源参照模式",LocalResourceReferenceMode);
        
		myScript = (BehaviorRunner)target;
        
        targetType = EditorGUILayout.TextField("targetType: ", targetType);
        
        if (myScript.GetNowState() != null)
        {
            EditorGUILayout.TextField("current: ", myScript.GetNowState().StateKey);
        }
        
        if (GUILayout.Button(" refresh skill define "))
        {
            StateIndexListOptions = LocalResourceReferenceMode ? 
            GetBeheviourOptions(targetType).ToArray() : GetBeheviourOptions(targetType, myScript.skillEntityList).ToArray();
        }
        
        EditorGUILayout.BeginVertical();
        myScript.AI_States_path = EditorGUILayout.TextField("AI_States_path", myScript.AI_States_path);
        EditorGUILayout.EndVertical();

        // --追加--
        if (myScript.skillEntityList != null)
        {
            if (!isInitialized) InitializeList(myScript.skillEntityList.Count);
        }else{
            myScript.skillEntityList = new List<SkillEntity>();
            if (!isInitialized) InitializeList(myScript.skillEntityList.Count);
        }
        // --ここまで--

        if (state_folding_list = EditorGUILayout.Foldout(state_folding_list, "States"))
        {
            casualToStateKeyOptionsList = new List<string>();
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical();
            for (int i = 0; i < myScript.skillEntityList.Count; i++)
            {
                if (!casualToStateKeyOptionsList.Contains(myScript.skillEntityList[i].REAL_NAME))
                    casualToStateKeyOptionsList.Add(myScript.skillEntityList[i].REAL_NAME);

                EditorGUI.indentLevel++;

                if (stateKeyGUI == null)
                    stateKeyGUI = new GUIStyle(GUI.skin.label);
                stateKeyGUI.normal.textColor = new Color(0.6f, 0.3f, 0.4f);
                myScript.skillEntityList[i].REAL_NAME =
                StateIndexListOptions.Contains(myScript.skillEntityList[i].REAL_NAME) ?
                StateIndexListOptions[EditorGUILayout.Popup("State Key", Array.IndexOf(StateIndexListOptions, myScript.skillEntityList[i].REAL_NAME), StateIndexListOptions, stateKeyGUI)] :
                StateIndexListOptions.Length > 0 ? StateIndexListOptions[0] : null;

                myScript.skillEntityList[i].StateType =
                (BehaviorType)EditorGUILayout.EnumPopup("Attack Type", myScript.skillEntityList[i].StateType);

                if (myScript.skillEntityList[i].StateType != BehaviorType.NONE || myScript.skillEntityList[i].StateType != BehaviorType.MV ||
                        myScript.skillEntityList[i].StateType != BehaviorType.Def || myScript.skillEntityList[i].StateType != BehaviorType.Hit ||
                            myScript.skillEntityList[i].StateType != BehaviorType.KnockOff)
                {
                    GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
                    if (attackRangeToggleGUI == null)
                    {
                        attackRangeToggleGUI = new GUIStyle(GUI.skin.toggle)
                        {
                            margin = new RectOffset(50, 22, 11, 11)
                        };
                        attackRangeToggleGUI.alignment = TextAnchor.MiddleLeft;
                        attackRangeToggleGUI.stretchWidth = false;
                    }

                    myScript.skillEntityList[i].AIAttrs.AI_MIN_DIS = EditorGUILayout.FloatField("Distance Min",myScript.skillEntityList[i].AIAttrs.AI_MIN_DIS);
                    myScript.skillEntityList[i].AIAttrs.AI_MAX_DIS = EditorGUILayout.FloatField("Distance Max",myScript.skillEntityList[i].AIAttrs.AI_MAX_DIS);
                    
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.BeginVertical();
                if (casualToFoldings[i] = EditorGUILayout.Foldout(casualToFoldings[i], " ****************** Casual To States ******************"))
                {
                    for (int y = 0; y < myScript.skillEntityList[i].CasualTo.Length; y++)
                    {
                        EditorGUI.indentLevel++;
                        if (casualToStateKeyOptions.Contains(myScript.skillEntityList[i].CasualTo[y]))
                        {
                            stateKeyGUI.normal.textColor = new Color(0.2f, 0.7f, 0.5f);                        
                            myScript.skillEntityList[i].CasualTo[y] =
                            casualToStateKeyOptions[EditorGUILayout.Popup(
                            "Casual To State Key",
                            Array.IndexOf(casualToStateKeyOptions, myScript.skillEntityList[i].CasualTo[y]),
                            casualToStateKeyOptions,
                            stateKeyGUI)];
                        }
                        else
                        {
                            myScript.skillEntityList[i].CasualTo[y] = casualToStateKeyOptions[0];
                        }
                        
                        stateKeyGUI.normal.textColor = new Color(0.6f, 0.3f, 0.4f);
                        deleteCasualToButtonStyle = new GUIStyle(GUI.skin.box);
                        deleteCasualToButtonStyle.normal.textColor = Color.blue;
                        deleteCasualToButtonStyle.alignment = TextAnchor.MiddleCenter;
                        deleteCasualToButtonStyle.margin = new RectOffset(50, 22, 11, 11);
                        if (GUILayout.Button("DeleteThis", deleteCasualToButtonStyle))
                        {
                            List<string> casualStateList = myScript.skillEntityList[i].CasualTo.ToList();
                            casualStateList.RemoveAt(y);
                            myScript.skillEntityList[i].CasualTo = casualStateList.ToArray();
                            EditorGUI.indentLevel--;
                            break;
                        }
                        EditorGUI.indentLevel--;
                    }
                    
                    addCasualToButtonStyle = new GUIStyle(GUI.skin.box);
                    addCasualToButtonStyle.normal.textColor = Color.red;
                    addCasualToButtonStyle.alignment = TextAnchor.MiddleCenter;
                    addCasualToButtonStyle.margin = new RectOffset(100, 22, 11, 11);

                    for (int z = 0; z < myScript.skillEntityList.Count;z++)
                    {
                        if (myScript.skillEntityList[z] != myScript.skillEntityList[i])
                        {
                            EditorGUI.indentLevel++;
                            if (GUILayout.Button("  +  " + myScript.skillEntityList[z].REAL_NAME, addCasualToButtonStyle))
                            {
                                List<string> casualStateList = myScript.skillEntityList[i].CasualTo.ToList();
                                casualStateList.Add(myScript.skillEntityList[z].REAL_NAME);
                                myScript.skillEntityList[i].CasualTo = casualStateList.ToArray();
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                EditorGUILayout.EndVertical();
                
                // 强制Force迁移
                EditorGUILayout.BeginVertical();
                if (forceToFoldings[i] = EditorGUILayout.Foldout(forceToFoldings[i], " !!! Force To States !!!"))
                {
                    try {
                        for (int y = 0; y < myScript.skillEntityList[i].ForcedTransitions.Length; y++)
                        {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.TextField("forceTo: ", myScript.skillEntityList[i].ForcedTransitions[y]);
                        EditorGUI.indentLevel--;
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.Log(e);
                    }
                }
                EditorGUILayout.EndVertical();
                
                myScript.skillEntityList[i].CAN_BE_CANCELLED_TO = EditorGUILayout.Toggle("superCancel", myScript.skillEntityList[i].CAN_BE_CANCELLED_TO);
                myScript.skillEntityList[i].EnterInput = (InputKey)EditorGUILayout.EnumPopup("enter input", myScript.skillEntityList[i].EnterInput);
                myScript.skillEntityList[i].ExitInput = (InputKey)EditorGUILayout.EnumPopup("exit input", myScript.skillEntityList[i].ExitInput);
                myScript.skillEntityList[i].SP_LEVEL = EditorGUILayout.IntPopup("SPLevel", myScript.skillEntityList[i].SP_LEVEL,exoptions_display,exoptions);
                GUI.backgroundColor = Color.blue;
                
                ButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { textColor = Color.white },
                    fixedWidth = 100f
                };
                if (GUILayout.Button("Delete",ButtonStyle))
                {
                    myScript.skillEntityList.RemoveAt(i);
                    InitializeList(i, myScript.skillEntityList.Count);
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.indentLevel--;
                GUILayout.Space(1f);
            }
            EditorGUILayout.EndVertical();
                    
            casualToStateKeyOptions = casualToStateKeyOptionsList.ToArray();
            
            if (GUILayout.Button("Add"))
            {
                GUI.color = Color.green;
                myScript.skillEntityList.Add(new SkillEntity("Empty", 0, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, 0));
                InitializeList(-1, myScript.skillEntityList.Count);
            }
        }
        
        GUI.backgroundColor = Color.green; 
		if(GUILayout.Button("saveTrans"))
		{
			myScript.SaveTrans(targetType);
		}
        GUI.backgroundColor = Color.white; 
	}

    bool isInitialized;
    bool state_folding_list;
    bool[] casualToFoldings;
    bool[] forceToFoldings;
    bool[] foldings;

    // Listの長さを初期化
    void InitializeList(int count)
    {
        foldings = new bool[count];
        casualToFoldings = new bool[count];
        forceToFoldings = new bool[count];
        isInitialized = true;
    }

    // 指定した番号以外をキャッシュして初期化 (i = -1の時は全てキャッシュして初期化)
    void InitializeList(int i, int count)
    {
        bool[] foldings_temp = foldings;
        foldings = new bool[count];

        for (int k = 0, j = 0; k < count; k++)
        {
            if (i == j) j++;
            if (foldings_temp.Length - 1 < j) break;
            foldings[k] = foldings_temp[j++];
        }
        ////////////////////////////////////////////
        bool[] foldings_temp2 = casualToFoldings;
        casualToFoldings = new bool[count];

        for (int k = 0, j = 0; k < count; k++)
        {
            if (i == j) j++;
            if (foldings_temp2.Length - 1 < j) break;
            casualToFoldings[k] = foldings_temp2[j++];
        }
        ////////////////////////////////////////////
        bool[] foldings_temp3 = forceToFoldings;
        forceToFoldings = new bool[count];

        for (int k = 0, j = 0; k < count; k++)
        {
            if (i == j) j++;
            if (foldings_temp3.Length - 1 < j) break;
            forceToFoldings[k] = foldings_temp3[j++];
        }
    }
}
#endif

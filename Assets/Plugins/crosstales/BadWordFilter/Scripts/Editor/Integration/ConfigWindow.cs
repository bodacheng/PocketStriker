#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Crosstales.BWF.EditorUtil;
using Crosstales.BWF.Util;

namespace Crosstales.BWF.EditorIntegration
{
   /// <summary>Editor window extension.</summary>
   public class ConfigWindow : ConfigBase
   {
      #region Variables

      private int tab;
      private int lastTab;
      private string inputText = "MARTIANS are asses.... => watch mypage.com";
      private string outputText;

      private Vector2 scrollPosPrefabs;
      private Vector2 scrollPosTD;

      #endregion


      #region EditorWindow methods

      [MenuItem("Tools/" + Constants.ASSET_NAME + "/ Configuration...", false, EditorHelper.MENU_ID + 1)]
      public static void ShowWindow()
      {
         GetWindow(typeof(ConfigWindow));
      }

      public static void ShowWindow(int tab)
      {
         ConfigWindow window = GetWindow(typeof(ConfigWindow)) as ConfigWindow;
         if (window != null) window.tab = tab;
      }

      private void OnEnable()
      {
         titleContent = new GUIContent(Constants.ASSET_NAME_SHORT, EditorHelper.Logo_Asset_Small);
      }

      private void OnGUI()
      {
         tab = GUILayout.Toolbar(tab, new[] { "Config", "Prefabs", "TD", "Help", "About" });

         if (tab != lastTab)
         {
            lastTab = tab;
            GUI.FocusControl(null);
         }

         switch (tab)
         {
            case 0:
            {
               showConfiguration();

               EditorHelper.SeparatorUI();

               GUILayout.BeginHorizontal();
               {
                  if (GUILayout.Button(new GUIContent(" Save", EditorHelper.Icon_Save, "Saves the configuration settings for this project.")))
                  {
                     save();
                  }

                  if (GUILayout.Button(new GUIContent(" Reset", EditorHelper.Icon_Reset, "Resets the configuration settings for this project.")))
                  {
                     if (EditorUtility.DisplayDialog("Reset configuration?", $"Reset the configuration of {Constants.ASSET_NAME}?", "Yes", "No"))
                     {
                        Config.Reset();
                        EditorConfig.Reset();
                        save();
                     }
                  }
               }
               GUILayout.EndHorizontal();

               GUILayout.Space(6);
               break;
            }
            case 1:
               showPrefabs();
               break;
            case 2:
               showTestDrive();
               break;
            case 3:
               showHelp();
               break;
            default:
               showAbout();
               break;
         }
      }

      private void OnInspectorUpdate()
      {
         Repaint();
      }

      #endregion


      #region Private methods

      private void showPrefabs()
      {
         scrollPosPrefabs = EditorGUILayout.BeginScrollView(scrollPosPrefabs, false, false);
         {
            GUILayout.Label("Available Prefabs", EditorStyles.boldLabel);

            GUILayout.Space(6);

            GUI.enabled = !EditorHelper.isBWFInScene;

            GUILayout.Label(Constants.MANAGER_SCENE_OBJECT_NAME);

            if (GUILayout.Button(new GUIContent(" Add", EditorHelper.Icon_Plus, $"Adds a '{Constants.MANAGER_SCENE_OBJECT_NAME}'-prefab to the scene.")))
            {
               EditorHelper.InstantiatePrefab(Constants.MANAGER_SCENE_OBJECT_NAME);
            }

            GUI.enabled = true;

            if (EditorHelper.isBWFInScene)
            {
               GUILayout.Space(6);
               EditorGUILayout.HelpBox("All available prefabs are already in the scene.", MessageType.Info);
            }

            GUILayout.Space(6);
         }
         EditorGUILayout.EndScrollView();
      }

      private void showTestDrive()
      {
         GUILayout.Space(3);
         GUILayout.Label("Test-Drive", EditorStyles.boldLabel);

         if (Helper.isEditorMode)
         {
            //if (BWFManager.isReady && EditorHelper.isBWFInScene)
            if (EditorHelper.isBWFInScene)
            {
               scrollPosTD = EditorGUILayout.BeginScrollView(scrollPosTD, false, false);
               {
                  inputText = EditorGUILayout.TextField(new GUIContent("Input Text", "Text to check."), inputText);

                  EditorHelper.ReadOnlyTextField("Output Text", outputText);
               }
               EditorGUILayout.EndScrollView();

               EditorHelper.SeparatorUI();

               GUILayout.BeginHorizontal();
               {
                  if (GUILayout.Button(new GUIContent(" Contains", EditorHelper.Icon_Contains, "Contains any bad words?")))
                  {
                     BWFManager.Instance.Load();
                     outputText = BWFManager.Instance.Contains(inputText).ToString();
                  }

                  if (GUILayout.Button(new GUIContent(" Get", EditorHelper.Icon_Get, "Get all bad words.")))
                  {
                     BWFManager.Instance.Load();
                     outputText = string.Join(", ", BWFManager.Instance.GetAll(inputText).ToArray());
                  }

                  if (GUILayout.Button(new GUIContent(" Replace", EditorHelper.Icon_Replace, "Check and replace all bad words.")))
                  {
                     BWFManager.Instance.Load();
                     outputText = BWFManager.Instance.ReplaceAll(inputText);
                  }

                  if (GUILayout.Button(new GUIContent(" Mark", EditorHelper.Icon_Mark, "Mark all bad words.")))
                  {
                     BWFManager.Instance.Load();
                     outputText = BWFManager.Instance.Mark(inputText);
                  }
               }
               GUILayout.EndHorizontal();
            }
            else
            {
               EditorHelper.BWFUnavailable();
            }
         }
         else
         {
            EditorGUILayout.HelpBox("Disabled in Play-mode!", MessageType.Info);
         }
      }

      #endregion
   }
}
#endif
// © 2016-2022 crosstales LLC (https://www.crosstales.com)
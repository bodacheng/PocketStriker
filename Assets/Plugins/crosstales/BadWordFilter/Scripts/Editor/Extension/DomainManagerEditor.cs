#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crosstales.BWF.EditorUtil;

namespace Crosstales.BWF.EditorExtension
{
   /// <summary>Custom editor for the 'DomainManager'-class.</summary>
   [CustomEditor(typeof(Crosstales.BWF.Manager.DomainManager))]
   public class DomainManagerEditor : Editor
   {
      #region Variables

      private Crosstales.BWF.Manager.DomainManager script;

      private string inputText = "Write me an email bwfuser@mypage.com!";
      private string outputText;

      private static bool showStats;
      private static bool showTD;

      #endregion


      #region Editor methods

      private void OnEnable()
      {
         script = (Crosstales.BWF.Manager.DomainManager)target;

         if (script.isActiveAndEnabled)
            script.Load();
      }

      public override void OnInspectorGUI()
      {
         DrawDefaultInspector();

         EditorHelper.SeparatorUI();

         if (script.isActiveAndEnabled)
         {
            EditorStyles.foldout.fontStyle = FontStyle.Bold;
            showStats = EditorGUILayout.Foldout(showStats, "Stats");
            EditorStyles.foldout.fontStyle = FontStyle.Normal;

            if (showStats)
            {
               EditorGUI.indentLevel++;

               GUILayout.Label($"Ready:\t\t{(script.isReady ? "Yes" : "No")}");

               if (script.isReady)
               {
                  GUILayout.Label($"Sources:\t{script.Sources.Count}");
                  GUILayout.Label($"Regex Count:\t{script.TotalRegexCount}");

                  EditorGUI.indentLevel--;
               }
            }

            EditorHelper.SeparatorUI();

            if (script.DomainProvider?.Count > 0)
            {
               EditorStyles.foldout.fontStyle = FontStyle.Bold;
               showTD = EditorGUILayout.Foldout(showTD, "Test-Drive");
               EditorStyles.foldout.fontStyle = FontStyle.Normal;

               if (showTD)
               {
                  EditorGUI.indentLevel++;

                  if (Crosstales.BWF.Util.Helper.isEditorMode)
                  {
                     inputText = EditorGUILayout.TextField(new GUIContent("Input Text", "Text to check."), inputText);

                     EditorHelper.ReadOnlyTextField("Output Text", outputText);

                     GUILayout.Space(8);

                     GUILayout.BeginHorizontal();
                     {
                        if (GUILayout.Button(new GUIContent(" Contains", EditorHelper.Icon_Contains, "Contains any domains?")))
                           outputText = script.Contains(inputText).ToString();

                        if (GUILayout.Button(new GUIContent(" Get", EditorHelper.Icon_Get, "Get all domains.")))
                           outputText = string.Join(", ", script.GetAll(inputText).ToArray());

                        if (GUILayout.Button(new GUIContent(" Replace", EditorHelper.Icon_Replace, "Check and replace all domains.")))
                           outputText = script.ReplaceAll(inputText);

                        if (GUILayout.Button(new GUIContent(" Mark", EditorHelper.Icon_Mark, "Mark all domains.")))
                           outputText = script.Mark(inputText);
                     }
                     GUILayout.EndHorizontal();
                  }
                  else
                  {
                     EditorGUILayout.HelpBox("Disabled in Play-mode!", MessageType.Info);
                  }

                  EditorGUI.indentLevel--;
               }
            }
            else
            {
               EditorGUILayout.HelpBox("Please add a 'Domain Provider'!", MessageType.Warning);
            }
         }
         else
         {
            EditorGUILayout.HelpBox("Script is disabled!", MessageType.Info);
         }
      }

      public override bool RequiresConstantRepaint()
      {
         return true;
      }

      #endregion
   }
}
#endif
// © 2016-2022 crosstales LLC (https://www.crosstales.com)
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crosstales.BWF.EditorUtil;

namespace Crosstales.BWF.EditorExtension
{
   /// <summary>Custom editor for the 'CapitalizationManager'-class.</summary>
   [CustomEditor(typeof(Crosstales.BWF.Manager.CapitalizationManager))]
   public class CapitalizationManagerEditor : Editor
   {
      #region Variables

      private Crosstales.BWF.Manager.CapitalizationManager script;

      private string inputText = "COME ON, TEST ME User!";
      private string outputText;

      private static bool showTD;

      #endregion


      #region Editor methods

      private void OnEnable()
      {
         script = (Crosstales.BWF.Manager.CapitalizationManager)target;

         if (script.isActiveAndEnabled)
            script.Load();
      }

      public override void OnInspectorGUI()
      {
         DrawDefaultInspector();

         EditorHelper.SeparatorUI();

         if (script.isActiveAndEnabled)
         {
            if (script.isReady)
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
                        if (GUILayout.Button(new GUIContent(" Contains", EditorHelper.Icon_Contains, "Contains any extensive capitalizations?")))
                           outputText = script.Contains(inputText).ToString();

                        if (GUILayout.Button(new GUIContent(" Get", EditorHelper.Icon_Get, "Get all extensive capitalizations.")))
                           outputText = string.Join(", ", script.GetAll(inputText).ToArray());

                        if (GUILayout.Button(new GUIContent(" Replace", EditorHelper.Icon_Replace, "Check and replace all extensive capitalizations.")))
                           outputText = script.ReplaceAll(inputText);

                        if (GUILayout.Button(new GUIContent(" Mark", EditorHelper.Icon_Mark, "Mark all extensive capitalizations.")))
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
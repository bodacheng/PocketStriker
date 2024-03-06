#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crosstales.BWF.EditorUtil;

namespace Crosstales.BWF.EditorExtension
{
   /// <summary>Custom editor for the 'Source'-class.</summary>
   [CustomEditor(typeof(Crosstales.BWF.Data.Source))]
   public class SourceEditor : Editor
   {
      #region Variables

      private Crosstales.BWF.Data.Source script;

      #endregion


      #region Editor methods

      private void OnEnable()
      {
         script = (Crosstales.BWF.Data.Source)target;
      }

      public override void OnInspectorGUI()
      {
         DrawDefaultInspector();


         if (string.IsNullOrEmpty(script.SourceName))
            UnityEditor.EditorGUILayout.HelpBox("The 'Source Name' is empty! Please add a name.", UnityEditor.MessageType.Warning);

         if (!Crosstales.Common.Util.NetworkHelper.isValidURL(script.URL) && script.Resource == null)
            UnityEditor.EditorGUILayout.HelpBox("The 'URL' or 'Resource' is empty or invalid! Please add a source.", UnityEditor.MessageType.Warning);

         EditorHelper.SeparatorUI();

         GUILayout.Label("Stats", EditorStyles.boldLabel);
         GUILayout.Label($"Regex Count:\t{script.RegexCount}");

         if (GUI.changed)
         {
            //Debug.Log("Changed");
            UnityEditor.EditorUtility.SetDirty(script);
            UnityEditor.AssetDatabase.SaveAssets();
         }
      }

      #endregion
   }
}
#endif
// © 2020-2022 crosstales LLC (https://www.crosstales.com)
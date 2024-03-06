#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Crosstales.BWF.EditorUtil
{
   /// <summary>Editor helper class.</summary>
   public abstract class EditorHelper : Crosstales.Common.EditorUtil.BaseEditorHelper
   {
      #region Static variables

      /// <summary>Start index inside the "GameObject"-menu.</summary>
      public const int GO_ID = 20;

      /// <summary>Start index inside the "Tools"-menu.</summary>
      public const int MENU_ID = 10201; // 1, B = 02, A = 01

      private static Texture2D logo_asset;
      private static Texture2D logo_asset_small;

      private static Texture2D icon_contains;
      private static Texture2D icon_get;
      private static Texture2D icon_replace;
      private static Texture2D icon_mark;

      #endregion


      #region Static properties

      public static Texture2D Logo_Asset => loadImage(ref logo_asset, "logo_asset_pro.png");

      public static Texture2D Logo_Asset_Small => loadImage(ref logo_asset_small, "logo_asset_small_pro.png");

      public static Texture2D Icon_Contains => loadImage(ref icon_contains, "icon_contains.png");

      public static Texture2D Icon_Get => loadImage(ref icon_get, "icon_get.png");

      public static Texture2D Icon_Replace => loadImage(ref icon_replace, "icon_replace.png");

      public static Texture2D Icon_Mark => loadImage(ref icon_mark, "icon_mark.png");

      #endregion


      #region Static methods

      /// <summary>Shows a "BWF unavailable"-UI.</summary>
      public static void BWFUnavailable()
      {
         EditorGUILayout.HelpBox("Bad Word Filter not available!", MessageType.Warning);

         if (isBWFInScene)
         {
            EditorGUILayout.HelpBox("BWF not ready - please wait...", MessageType.Info);
         }
         else
         {
            EditorGUILayout.HelpBox($"Did you add the '{Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME}'-prefab to the scene?", MessageType.Info);

            GUILayout.Space(8);

            if (GUILayout.Button(new GUIContent($" Add {Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME}", Icon_Plus, $"Add the '{Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME}'-prefab to the current scene.")))
            {
               InstantiatePrefab(Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME);
            }
         }
      }

      /// <summary>Instantiates a prefab.</summary>
      /// <param name="prefabName">Name of the prefab.</param>
      public static void InstantiatePrefab(string prefabName)
      {
         InstantiatePrefab(prefabName, EditorConfig.PREFAB_PATH);
      }

      /// <summary>Checks if the 'BWF'-prefab is in the scene.</summary>
      /// <returns>True if the 'BWF'-prefab is in the scene.</returns>
      public static bool isBWFInScene => GameObject.FindObjectOfType(typeof(BWFManager)) != null; //GameObject.Find(Util.Constants.MANAGER_SCENE_OBJECT_NAME) != null;

      /// <summary>Loads an image as Texture2D from 'Editor Default Resources'.</summary>
      /// <param name="logo">Logo to load.</param>
      /// <param name="fileName">Name of the image.</param>
      /// <returns>Image as Texture2D from 'Editor Default Resources'.</returns>
      private static Texture2D loadImage(ref Texture2D logo, string fileName)
      {
         if (logo == null)
         {
#if CT_DEVELOP
            logo = (Texture2D)AssetDatabase.LoadAssetAtPath($"Assets{EditorConfig.ASSET_PATH}Icons/{fileName}", typeof(Texture2D));
#else
            logo = (Texture2D)EditorGUIUtility.Load($"crosstales/BadWordFilter/{fileName}");
#endif

            if (logo == null)
            {
               Debug.LogWarning($"Image not found: {fileName}");
            }
         }

         return logo;
      }

      #endregion
   }
}
#endif
// © 2016-2022 crosstales LLC (https://www.crosstales.com)
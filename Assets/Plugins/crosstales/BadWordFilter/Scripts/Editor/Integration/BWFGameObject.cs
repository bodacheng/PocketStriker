#if UNITY_EDITOR
using UnityEditor;
using Crosstales.BWF.EditorUtil;

namespace Crosstales.BWF.EditorIntegration
{
   /// <summary>Editor component for the "Hierarchy"-menu.</summary>
   public static class BWFGameObject
   {
      [MenuItem("GameObject/" + Crosstales.BWF.Util.Constants.ASSET_NAME + "/" + Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME, false, EditorHelper.GO_ID)]
      private static void AddRadioPlayer()
      {
         EditorHelper.InstantiatePrefab(Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME);
      }

      [MenuItem("GameObject/" + Crosstales.BWF.Util.Constants.ASSET_NAME + "/" + Crosstales.BWF.Util.Constants.MANAGER_SCENE_OBJECT_NAME, true)]
      private static bool AddBWFValidator()
      {
         return !EditorHelper.isBWFInScene;
      }
   }
}
#endif
// © 2017-2022 crosstales LLC (https://www.crosstales.com)
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

namespace Crosstales.BWF.EditorTask
{
   /// <summary>Show the configuration window on the first launch.</summary>
   public class Launch : AssetPostprocessor
   {
      public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
      {
         if (importedAssets.Any(str => str.Contains(Crosstales.BWF.EditorUtil.EditorConstants.ASSET_UID.ToString())))
         {
            Crosstales.Common.EditorTask.SetupResources.Setup();
            SetupResources.Setup();

            Crosstales.BWF.EditorIntegration.ConfigWindow.ShowWindow(4);
         }
      }
   }
}
#endif
// © 2017-2022 crosstales LLC (https://www.crosstales.com)
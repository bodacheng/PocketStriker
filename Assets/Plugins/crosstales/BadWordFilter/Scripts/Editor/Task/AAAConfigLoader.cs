#if UNITY_EDITOR
using UnityEditor;

namespace Crosstales.BWF.EditorTask
{
   /// <summary>Loads the configuration at startup.</summary>
   [InitializeOnLoad]
   public static class AAAConfigLoader
   {
      #region Constructor

      static AAAConfigLoader()
      {
         if (!Crosstales.BWF.Util.Config.isLoaded)
         {
            Crosstales.BWF.Util.Config.Load();

            if (Crosstales.BWF.Util.Config.DEBUG)
               UnityEngine.Debug.Log("Config data loaded");
         }
      }

      #endregion
   }
}
#endif
// © 2017-2022 crosstales LLC (https://www.crosstales.com)
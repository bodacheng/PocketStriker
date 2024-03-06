using UnityEngine;

namespace Crosstales.BWF.Util
{
   /// <summary>Setup the project to use BWF.</summary>
#if UNITY_EDITOR
   [UnityEditor.InitializeOnLoadAttribute]
#endif
   public class SetupProject
   {
      #region Constructor

      static SetupProject()
      {
         setup();
      }

      #endregion


      #region Public methods

      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
      private static void setup()
      {
         Crosstales.Common.Util.Singleton<BWFManager>.PrefabPath = "Prefabs/BWF";
         Crosstales.Common.Util.Singleton<BWFManager>.GameObjectName = "BWF";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.BadWordManager>.PrefabPath = "Prefabs/BadWordManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.BadWordManager>.GameObjectName = "BadWordManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.CapitalizationManager>.PrefabPath = "Prefabs/CapitalizationManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.CapitalizationManager>.GameObjectName = "CapitalizationManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.DomainManager>.PrefabPath = "Prefabs/DomainManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.DomainManager>.GameObjectName = "DomainManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.PunctuationManager>.PrefabPath = "Prefabs/PunctuationManager";
         Crosstales.Common.Util.Singleton<Crosstales.BWF.Manager.PunctuationManager>.GameObjectName = "PunctuationManager";
      }

      #endregion
   }
}
// © 2020-2022 crosstales LLC (https://www.crosstales.com)
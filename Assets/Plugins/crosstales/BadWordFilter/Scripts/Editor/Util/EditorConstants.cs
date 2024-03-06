#if UNITY_EDITOR
using Crosstales.BWF.Util;

namespace Crosstales.BWF.EditorUtil
{
   /// <summary>Collected editor constants of very general utility for the asset.</summary>
   public static class EditorConstants
   {
      #region Constant variables

      // Keys for the configuration of the asset
      public const string KEY_UPDATE_CHECK = Constants.KEY_PREFIX + "UPDATE_CHECK";
      public const string KEY_COMPILE_DEFINES = Constants.KEY_PREFIX + "COMPILE_DEFINES";
      public const string KEY_PREFAB_AUTOLOAD = Constants.KEY_PREFIX + "PREFAB_AUTOLOAD";

      public const string KEY_HIERARCHY_ICON = Constants.KEY_PREFIX + "HIERARCHY_ICON";

      public const string KEY_UPDATE_DATE = Constants.KEY_PREFIX + "UPDATE_DATE";


      // Default values
      public const string DEFAULT_ASSET_PATH = "/Plugins/crosstales/BadWordFilter/";
      public const bool DEFAULT_UPDATE_CHECK = false;
      public const bool DEFAULT_COMPILE_DEFINES = true;
      public const bool DEFAULT_PREFAB_AUTOLOAD = false;
      public const bool DEFAULT_HIERARCHY_ICON = false;

      #endregion


      #region Changable variables

      /// <summary>Sub-path to the prefabs.</summary>
      public static string PREFAB_SUBPATH = "Resources/Prefabs/";

      #endregion


      #region Properties

      /// <summary>Returns the URL of the asset in UAS.</summary>
      /// <returns>The URL of the asset in UAS.</returns>
      public static string ASSET_URL => Constants.ASSET_PRO_URL;

      /// <summary>Returns the ID of the asset in UAS.</summary>
      /// <returns>The ID of the asset in UAS.</returns>
      public static string ASSET_ID => "26255";

      /// <summary>Returns the UID of the asset.</summary>
      /// <returns>The UID of the asset.</returns>
      public static System.Guid ASSET_UID => new System.Guid("b11eebc0-525a-4d58-b33d-c0a9a728f3a9");

      #endregion
   }
}
#endif
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
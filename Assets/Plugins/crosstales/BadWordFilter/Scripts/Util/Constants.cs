namespace Crosstales.BWF.Util
{
   /// <summary>Collected constants of very general utility for the asset.</summary>
   public abstract class Constants : Crosstales.Common.Util.BaseConstants
   {
      #region Constant variables

      /// <summary>Name of the asset.</summary>
      public const string ASSET_NAME = "Bad Word Filter PRO";

      /// <summary>Short name of the asset.</summary>
      public const string ASSET_NAME_SHORT = "BWF PRO";

      /// <summary>Version of the asset.</summary>
      public const string ASSET_VERSION = "2022.2.0";

      /// <summary>Build number of the asset.</summary>
      public const int ASSET_BUILD = 20220815;

      /// <summary>Create date of the asset (YYYY, MM, DD).</summary>
      public static readonly System.DateTime ASSET_CREATED = new System.DateTime(2015, 1, 3);

      /// <summary>Change date of the asset (YYYY, MM, DD).</summary>
      public static readonly System.DateTime ASSET_CHANGED = new System.DateTime(2022, 8, 15);

      /// <summary>URL of the PRO asset in UAS.</summary>
      public const string ASSET_PRO_URL = "https://assetstore.unity.com/packages/slug/26255?aid=1011lNGT";

      /// <summary>URL for update-checks of the asset</summary>
      public const string ASSET_UPDATE_CHECK_URL = "https://www.crosstales.com/media/assets/bwf_versions.txt";
      //public const string ASSET_UPDATE_CHECK_URL = "https://www.crosstales.com/media/assets/test/bwf_versions_test.txt";

      /// <summary>Contact to the owner of the asset.</summary>
      public const string ASSET_CONTACT = "bwf@crosstales.com";

      /// <summary>URL of the asset manual.</summary>
      public const string ASSET_MANUAL_URL = "https://www.crosstales.com/media/data/assets/badwordfilter/BadWordFilter-doc.pdf";

      /// <summary>URL of the asset API.</summary>
      public const string ASSET_API_URL = "https://www.crosstales.com/en/assets/badwordfilter/api";

      /// <summary>URL of the asset forum.</summary>
      public const string ASSET_FORUM_URL = "https://forum.unity.com/threads/bad-word-filter-pro-solution-against-profanity-and-obscenity.289960/";

      /// <summary>URL of the asset in crosstales.</summary>
      public const string ASSET_WEB_URL = "https://www.crosstales.com/en/portfolio//badwordfilter/";

      /// <summary>URL of the promotion video of the asset (Youtube).</summary>
      public const string ASSET_VIDEO_PROMO = "https://youtu.be/pXICeRKaRPM?list=PLgtonIOr6Tb41XTMeeZ836tjHlKgOO84S";

      /// <summary>URL of the tutorial video of the asset (Youtube).</summary>
      public const string ASSET_VIDEO_TUTORIAL = "https://youtu.be/W8FxFlIObWM?list=PLgtonIOr6Tb41XTMeeZ836tjHlKgOO84S";

      // Keys for the configuration of the asset
      public const string KEY_PREFIX = "BWF_CFG_";
      public const string KEY_DEBUG = KEY_PREFIX + "DEBUG";
      public const string KEY_DEBUG_BADWORDS = KEY_PREFIX + "DEBUG_BADWORDS";
      public const string KEY_DEBUG_DOMAINS = KEY_PREFIX + "DEBUG_DOMAINS";

      // Default values
      public const bool DEFAULT_DEBUG_BADWORDS = false;
      public const bool DEFAULT_DEBUG_DOMAINS = false;

      /// <summary>BWF prefab scene name.</summary>
      public const string MANAGER_SCENE_OBJECT_NAME = "BWF";

      #endregion


      #region Changable variables

      /// <summary>Timeout for the UnityWebRequest-class in seconds.</summary>
      public static int WWW_TIMEOUT = 5;

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
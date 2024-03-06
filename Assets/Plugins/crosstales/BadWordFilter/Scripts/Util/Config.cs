namespace Crosstales.BWF.Util
{
   /// <summary>Configuration for the asset.</summary>
   public static class Config
   {
      #region Variables

      /// <summary>Enable or disable debug logging for the asset.</summary>
      public static bool DEBUG = Constants.DEFAULT_DEBUG || Constants.DEV_DEBUG;

      /// <summary>Enable or disable debug logging for BadWords (Attention: slow!).</summary>
      public static bool DEBUG_BADWORDS = Constants.DEFAULT_DEBUG_BADWORDS;

      /// <summary>Enable or disable debug logging for Domains (Attention: VERY SLOOOOOOOOWWWW!).</summary>
      public static bool DEBUG_DOMAINS = Constants.DEFAULT_DEBUG_DOMAINS;

      /// <summary>Is the configuration loaded?</summary>
      public static bool isLoaded;

      #endregion

#if UNITY_EDITOR

      #region Public static methods

      /// <summary>Resets all changeable variables to their default value.</summary>
      public static void Reset()
      {
         if (!Constants.DEV_DEBUG)
            DEBUG = Constants.DEFAULT_DEBUG;

         DEBUG_BADWORDS = Constants.DEFAULT_DEBUG_BADWORDS;
         DEBUG_DOMAINS = Constants.DEFAULT_DEBUG_DOMAINS;
      }

      /// <summary>Loads all changeable variables.</summary>
      public static void Load()
      {
         if (!Constants.DEV_DEBUG)
         {
            if (Crosstales.Common.Util.CTPlayerPrefs.HasKey(Constants.KEY_DEBUG))
               DEBUG = Crosstales.Common.Util.CTPlayerPrefs.GetBool(Constants.KEY_DEBUG);
         }
         else
         {
            DEBUG = Constants.DEV_DEBUG;
         }

         if (Crosstales.Common.Util.CTPlayerPrefs.HasKey(Constants.KEY_DEBUG_BADWORDS))
            DEBUG_BADWORDS = Crosstales.Common.Util.CTPlayerPrefs.GetBool(Constants.KEY_DEBUG_BADWORDS);

         if (Crosstales.Common.Util.CTPlayerPrefs.HasKey(Constants.KEY_DEBUG_DOMAINS))
            DEBUG_DOMAINS = Crosstales.Common.Util.CTPlayerPrefs.GetBool(Constants.KEY_DEBUG_DOMAINS);

         isLoaded = true;
      }

      /// <summary>Saves all changeable variables.</summary>
      public static void Save()
      {
         if (!Constants.DEV_DEBUG)
            Crosstales.Common.Util.CTPlayerPrefs.SetBool(Constants.KEY_DEBUG, DEBUG);

         Crosstales.Common.Util.CTPlayerPrefs.SetBool(Constants.KEY_DEBUG_BADWORDS, DEBUG_BADWORDS);
         Crosstales.Common.Util.CTPlayerPrefs.SetBool(Constants.KEY_DEBUG_DOMAINS, DEBUG_DOMAINS);

         Crosstales.Common.Util.CTPlayerPrefs.Save();
      }

      #endregion

#endif
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
namespace Crosstales.BWF.Provider
{
   /// <summary>Interface for all providers.</summary>
   public interface IProvider
   {
      #region Properties

      /// <summary>Checks the readiness status of the provider.</summary>
      /// <returns>True if the provider is ready.</returns>
      bool isReady { get; set; }

      #endregion


      #region Methods

      /// <summary>Loads all sources.</summary>
      void Load();

      /// <summary>Saves all sources.</summary>
      void Save();

      #endregion
   }
}
// © 2018-2022 crosstales LLC (https://www.crosstales.com)
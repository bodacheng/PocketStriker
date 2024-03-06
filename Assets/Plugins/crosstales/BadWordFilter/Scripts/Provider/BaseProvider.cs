using UnityEngine;
using System.Linq;

namespace Crosstales.BWF.Provider
{
   /// <summary>Base class for all providers.</summary>
   [ExecuteInEditMode]
   public abstract class BaseProvider : MonoBehaviour, IProvider
   {
      #region Variables

      /// <summary>Option1 (default: RegexOptions.IgnoreCase).</summary>
      [Header("Regex Options")] [Tooltip("Option1 (default: RegexOptions.IgnoreCase).")] public System.Text.RegularExpressions.RegexOptions RegexOption1 = System.Text.RegularExpressions.RegexOptions.IgnoreCase; //DEFAULT

      /// <summary>Option2 (default: RegexOptions.CultureInvariant).</summary>
      [Tooltip("Option2 (default: RegexOptions.CultureInvariant).")] public System.Text.RegularExpressions.RegexOptions RegexOption2 = System.Text.RegularExpressions.RegexOptions.CultureInvariant; //DEFAULT

      /// <summary>Option3 (default: RegexOptions.None).</summary>
      [Tooltip("Option3 (default: RegexOptions.None).")] public System.Text.RegularExpressions.RegexOptions RegexOption3 = System.Text.RegularExpressions.RegexOptions.None;

      /// <summary>Option4 (default: RegexOptions.None).</summary>
      [Tooltip("Option4 (default: RegexOptions.None).")] public System.Text.RegularExpressions.RegexOptions RegexOption4 = System.Text.RegularExpressions.RegexOptions.None;

      /// <summary>Option5 (default: RegexOptions.None).</summary>
      [Tooltip("Option5 (default: RegexOptions.None).")] public System.Text.RegularExpressions.RegexOptions RegexOption5 = System.Text.RegularExpressions.RegexOptions.None;


      /// <summary>All sources for this provider.</summary>
      [Header("Sources")] [Tooltip("All sources for this provider.")] [ContextMenuItem("Create Source", "createSource")]
      //public Data.Source[] Sources;
      public System.Collections.Generic.List<Crosstales.BWF.Data.Source> Sources;


      /// <summary>Clears all existing bad words on 'Load' (default: true).</summary>
      [Header("Load Behaviour")] [Tooltip("Clears all existing bad words on 'Load' (default: true).")] public bool ClearOnLoad = true;

      //protected System.Collections.Generic.List<System.Guid> coRoutines = new System.Collections.Generic.List<System.Guid>();
      protected readonly System.Collections.Generic.List<string> coRoutines = new System.Collections.Generic.List<string>();

      protected static bool loggedUnsupportedPlatform = false;
      protected bool loading = false;

      #endregion


      #region Properties

      /// <summary>Number of Regex of this provider.</summary>
      /// <returns>Number of Regex of this provider.</returns>
      public int RegexCount
      {
         get { return Sources?.Sum(src => src != null ? src.RegexCount : 0) ?? 0; }
      }

      #endregion


      #region Implemented methods

      public bool isReady { get; set; }

      public abstract void Load();

      public abstract void Save();

      #endregion


      #region Abstract methods

      /// <summary>Initialize the provider.</summary>
      protected abstract void init();

      #endregion


      #region MonoBehaviour methods

      private void Awake()
      {
         Load();
      }

      #endregion


      #region Protected methods

      protected void logNoResourcesAdded()
      {
         Debug.LogWarning($"No 'Resources' for {name} added!{System.Environment.NewLine}If you want to use this functionality, please add your desired 'Resources'.", this);
      }

      protected void createSource()
      {
         Crosstales.BWF.Util.Helper.CreateSource();
      }

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
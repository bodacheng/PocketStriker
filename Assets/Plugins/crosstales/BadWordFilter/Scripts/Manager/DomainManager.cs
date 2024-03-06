using UnityEngine;
using System.Linq;
using System.Collections;
using Crosstales.BWF.Provider;
using Crosstales.BWF.Filter;

namespace Crosstales.BWF.Manager
{
   /// <summary>Manager for domains.</summary>
   [DisallowMultipleComponent]
   [HelpURL("https://www.crosstales.com/media/data/assets/badwordfilter/api/class_crosstales_1_1_b_w_f_1_1_manager_1_1_domain_manager.html")]
   public class DomainManager : Crosstales.BWF.Manager.BaseManager<DomainManager, DomainFilter>
   {
      #region Variables

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("ReplaceChars")] [Header("Specific Settings")] [Tooltip("Replace characters for domains (default: *)."), SerializeField]
      private string replaceChars = "*"; //e.g. "?#@*&%!$^~+-/<>:;=()[]{}"

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("DomainProvider")] [Header("Domain Providers")] [Tooltip("List of all domain providers."), SerializeField]
      private System.Collections.Generic.List<DomainProvider> domainProvider;


      [Header("Events")] public Crosstales.BWF.OnContainsCompleted OnContainsCompleted;
      public Crosstales.BWF.OnGetAllCompleted OnGetAllCompleted;
      public Crosstales.BWF.OnReplaceAllCompleted OnReplaceAllCompleted;

#if !UNITY_WSA && !UNITY_WEBGL
      private System.Threading.Thread worker;
#endif

      #endregion


      #region Properties

      /// <summary>Replace characters for domains.</summary>
      public string ReplaceChars
      {
         get => filter?.ReplaceCharacters ?? replaceChars;
         set => filter.ReplaceCharacters = replaceChars = value;
      }

      /// <summary>List of all domain providers.</summary>
      /// 
      public System.Collections.Generic.List<DomainProvider> DomainProvider
      {
         get => domainProvider;
         set => domainProvider = value;
      }

      /// <summary>Returns all sources for the manager.</summary>
      /// <returns>List with all sources for the manager</returns>
      public System.Collections.Generic.List<Crosstales.BWF.Data.Source> Sources => filter?.Sources;

      /// <summary>Total number of Regex of all providers and sources.</summary>
      /// <returns>Total number of Regex of all providers and sources.</returns>
      public int TotalRegexCount => Sources.Sum(src => src.RegexCount);

      protected override Crosstales.BWF.OnContainsCompleted onContainsCompleted => OnContainsCompleted;
      protected override Crosstales.BWF.OnGetAllCompleted onGetAllCompleted => OnGetAllCompleted;
      protected override Crosstales.BWF.OnReplaceAllCompleted onReplaceAllCompleted => OnReplaceAllCompleted;

      #endregion


      #region MonoBehaviour methods

      protected override void Awake()
      {
         base.Awake();

         if (Instance == this)
            Load();
      }

      private void OnValidate()
      {
         if (replaceChars != ReplaceChars)
            ReplaceChars = replaceChars;
      }

      protected override void OnApplicationQuit()
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive == true)
         {
            if (Crosstales.BWF.Util.Constants.DEV_DEBUG)
               Debug.Log("Kill worker!", this);

            worker.Abort();
         }
#endif

         base.OnApplicationQuit();
      }

      #endregion


      #region Public methods

      /// <summary>Resets this object.</summary>
      public static void ResetObject()
      {
         DeleteInstance();
      }

      /// <summary>Loads the current filter with all settings from this object.</summary>
      public void Load()
      {
         filter = new DomainFilter(domainProvider, replaceChars, DisableOrdering);
      }

      /// <summary>Searches for domains in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "iana", optional)</param>
      /// <returns>True if a match was found</returns>
      public bool Contains(string text, params string[] sourceNames)
      {
         bool result = false;

         if (!string.IsNullOrEmpty(text) && filter != null)
            result = filter.Contains(text, sourceNames);

         return result;
      }

      /// <summary>Searches asynchronously for domains in a text. Use the "OnContainsComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      public void ContainsAsync(string text, params string[] sourceNames)
      {
         StartCoroutine(containsAsync(text, sourceNames));
      }

      /// <summary>Searches for domains in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "iana", optional)</param>
      /// <returns>List with all the matches</returns>
      public System.Collections.Generic.List<string> GetAll(string text, params string[] sourceNames)
      {
         System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();

         if (!string.IsNullOrEmpty(text))
            result = filter?.GetAll(text, sourceNames);

         return result;
      }

      /// <summary>Searches asynchronously for domains in a text. Use the "OnGetAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      public void GetAllAsync(string text, params string[] sourceNames)
      {
         StartCoroutine(getAllAsync(text, sourceNames));
      }

      /// <summary>Searches and replaces all domains in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found domain (optional)</param>
      /// <param name="postfix">Postfix for every found domain (optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "iana", optional)</param>
      /// <returns>Clean text</returns>
      public string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
         string result = text;

         if (!string.IsNullOrEmpty(text))
            result = filter?.ReplaceAll(text, markOnly, prefix, postfix, sourceNames);

         return result;
      }

      /// <summary>Searches and replaces asynchronously all domains in a text. Use the "OnReplaceAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found domain (optional)</param>
      /// <param name="postfix">Postfix for every found domain (optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "iana", optional)</param>
      public void ReplaceAllAsync(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
         StartCoroutine(replaceAllAsync(text, markOnly, prefix, postfix, sourceNames));
      }

      /// <summary>Marks the text with a prefix and postfix.</summary>
      /// <param name="text">Text containing domains</param>
      /// <param name="replace">Replace the domains (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found domain (default: bold and red, optional)</param>
      /// <param name="postfix">Postfix for every found domain (default: bold and red, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "iana", optional)</param>
      /// <returns>Text with marked domains</returns>
      public string Mark(string text, bool replace = false, string prefix = "<b><color=red>", string postfix = "</color></b>", params string[] sourceNames)
      {
         string result = text;

         if (!string.IsNullOrEmpty(text))
               result = filter?.Mark(text, replace, prefix, postfix, sourceNames);

         return result;
      }

      #endregion


      #region Private methods

      private IEnumerator containsAsync(string text, params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            bool result = true;

            worker = new System.Threading.Thread(() => result = Contains(text, sourceNames));
            worker.Start();

            do
            {
               yield return null;
            } while (worker.IsAlive);

            onContainsComplete(text, result);
         }
#else
         Debug.LogWarning("'ContainsAsync' is not supported under the current platform!", this);
         yield return null;
#endif
      }

      private IEnumerator getAllAsync(string text, params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            System.Collections.Generic.List<string> result = null;

            worker = new System.Threading.Thread(() => result = GetAll(text, sourceNames));
            worker.Start();

            do
            {
               yield return null;
            } while (worker.IsAlive);

            onGetAllComplete(text, result);
         }
#else
         Debug.LogWarning("'GetAllAsync' is not supported under the current platform!", this);
         yield return null;
#endif
      }

      private IEnumerator replaceAllAsync(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            string result = null;

            worker = new System.Threading.Thread(() => result = ReplaceAll(text, markOnly, prefix, postfix, sourceNames));
            worker.Start();

            do
            {
               yield return null;
            } while (worker.IsAlive);

            onReplaceAllComplete(text, result);
         }
#else
         Debug.LogWarning("'ReplaceAllAsync' is not supported under the current platform!", this);
         yield return null;
#endif
      }

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
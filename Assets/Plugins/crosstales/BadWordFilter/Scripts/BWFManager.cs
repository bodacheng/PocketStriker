using UnityEngine;
using System.Linq;
using System.Collections;

namespace Crosstales.BWF
{
   /// <summary>BWF is a multi-manager for all available managers.</summary>
   [ExecuteInEditMode]
   [HelpURL("https://www.crosstales.com/media/data/assets/badwordfilter/api/class_crosstales_1_1_b_w_f_1_1_b_w_f_manager.html")]
   public class BWFManager : Crosstales.Common.Util.Singleton<BWFManager>
   {
      #region Variables

      [Tooltip("Disables the ordering of the 'GetAll'-method (prevent possible memory garbage).")] public bool DisableOrdering;

      private bool sentReady;

#if !UNITY_WSA && !UNITY_WEBGL
      private System.Threading.Thread worker;
#endif

      private readonly System.Collections.Generic.List<string> getAllResult = new System.Collections.Generic.List<string>();

      #endregion


      #region Properties

      /// <summary>Checks the readiness status of all managers.</summary>
      /// <returns>True if all managers are ready.</returns>
      public bool isReady => Manager.BadWordManager.Instance != null && Manager.BadWordManager.Instance.isReady
                                                                     && Manager.DomainManager.Instance != null && Manager.DomainManager.Instance.isReady
                                                                     && Manager.CapitalizationManager.Instance != null && Manager.CapitalizationManager.Instance.isReady
                                                                     && Manager.PunctuationManager.Instance != null && Manager.PunctuationManager.Instance.isReady;

      /// <summary>Total number of Regex.</summary>
      /// <returns>Total number of Regex.</returns>
      public int TotalRegexCount => Sources().Sum(src => src.RegexCount);

      #endregion


      #region Events

      [Header("Events")] public OnReady OnReady;
      public OnContainsCompleted OnContainsCompleted;
      public OnGetAllCompleted OnGetAllCompleted;
      public OnReplaceAllCompleted OnReplaceAllCompleted;

      public delegate void BWFReady();

      /// <summary>An event triggered whenever BWF is ready.</summary>
      public event BWFReady OnBWFReady;

      /// <summary>An event triggered whenever the "Contains"-operation is completed.</summary>
      public event ContainsComplete OnContainsComplete;

      /// <summary>An event triggered whenever the "GetAll"-operation is completed.</summary>
      public event GetAllComplete OnGetAllComplete;

      /// <summary>An event triggered whenever the "ReplaceAll"-operation is completed.</summary>
      public event ReplaceAllComplete OnReplaceAllComplete;

      #endregion


      #region MonoBehaviour methods

      protected override void OnApplicationQuit()
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive == true)
         {
            if (Util.Constants.DEV_DEBUG)
               Debug.Log("Kill worker!", this);

            worker.Abort();
         }
#endif

         base.OnApplicationQuit();
      }

      private void Update()
      {
         if (!sentReady && isReady)
         {
            sentReady = true;

            onBWFReady();
         }
      }

      #endregion


      #region Public methods

      /// <summary>Loads the filter of a manager.</summary>
      /// <param name="mask">Active manager (default: ManagerMask.All, optional)</param>
      public void Load(Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All)
      {
         if ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            Manager.BadWordManager.Instance.Load();

         if ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            Manager.DomainManager.Instance.Load();

         if ((mask & Model.Enum.ManagerMask.Capitalization) == Model.Enum.ManagerMask.Capitalization || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            Manager.CapitalizationManager.Instance.Load();

         if ((mask & Model.Enum.ManagerMask.Punctuation) == Model.Enum.ManagerMask.Punctuation || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            Manager.PunctuationManager.Instance.Load();
      }

      /// <summary>Returns all sources for a manager.</summary>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <returns>List with all sources for the selected manager</returns>
      public System.Collections.Generic.List<Data.Source> Sources(Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All)
      {
         System.Collections.Generic.List<Data.Source> result = new System.Collections.Generic.List<Data.Source>(30);

         if ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result.AddRange(Manager.BadWordManager.Instance.Sources);

         if ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result.AddRange(Manager.DomainManager.Instance.Sources);

         return result.Distinct().OrderBy(x => x.SourceName).ToList();
      }

      /// <summary>Searches for unwanted words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>True if a match was found</returns>
      public bool Contains(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         contains(out bool result, text, mask, sourceNames);

         onContainsComplete(text, result);

         return result;
      }

      /// <summary>Searches asynchronously for unwanted words in a text. Use the "OnContainsComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      public void ContainsAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         StartCoroutine(containsAsync(text, mask, sourceNames));
      }

      /// <summary>Searches for unwanted words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>List with all the matches</returns>
      public System.Collections.Generic.List<string> GetAll(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         getAll(out System.Collections.Generic.List<string> result, text, mask, sourceNames);

         onGetAllComplete(text, result);

         return result;
      }

      /// <summary>Searches asynchronously for unwanted words in a text. Use the "OnGetAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      public void GetAllAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         StartCoroutine(getAllAsync(text, mask, sourceNames));
      }

      /// <summary>Searches and replaces all unwanted words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>Clean text</returns>
      public string ReplaceAll(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         return ReplaceAll(text, mask, false, string.Empty, string.Empty, sourceNames);
      }

      /// <summary>Searches and replaces all unwanted words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found bad word (optional)</param>
      /// <param name="postfix">Postfix for every found bad word (optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>Clean text</returns>
      public string ReplaceAll(string text, Model.Enum.ManagerMask mask, bool markOnly, string prefix, string postfix, params string[] sourceNames)
      {
         replaceAll(out string result, text, mask, markOnly, prefix, postfix, sourceNames);

         onReplaceAllComplete(text, result);

         return result;
      }

      /// <summary>Searches and replaces asynchronously all unwanted words in a text. Use the "OnReplaceAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      public void ReplaceAllAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         ReplaceAllAsync(text, mask, false, string.Empty, string.Empty, sourceNames);
      }

      /// <summary>Searches and replaces asynchronously all unwanted words in a text. Use the "OnReplaceAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All)</param>
      /// <param name="markOnly">Only mark the words (default: false)</param>
      /// <param name="prefix">Prefix for every found bad word</param>
      /// <param name="postfix">Postfix for every found bad word</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english")</param>
      public void ReplaceAllAsync(string text, Model.Enum.ManagerMask mask, bool markOnly, string prefix, string postfix, params string[] sourceNames)
      {
         StartCoroutine(replaceAllAsync(text, mask, markOnly, prefix, postfix, sourceNames));
      }

      /// <summary>
      /// Marks the text with a prefix and postfix from a list of words.
      /// Use this method if you already have a list of bad words (e.g. from the 'GetAll()' method).
      /// </summary>
      /// <param name="text">Text containing unwanted words</param>
      /// <param name="unwantedWords">Unwanted words to mark</param>
      /// <param name="prefix">Prefix for every found unwanted word (optional)</param>
      /// <param name="postfix">Postfix for every found unwanted word (optional)</param>
      /// <returns>Text with marked unwanted words</returns>
      public string Mark(string text, System.Collections.Generic.List<string> unwantedWords, string prefix = "<b><color=red>", string postfix = "</color></b>")
      {
         string result = text;

         string _prefix = prefix;
         string _postfix = postfix;

         if (string.IsNullOrEmpty(text))
         {
            if (Util.Constants.DEV_DEBUG)
               Debug.LogWarning($"Parameter 'text' is null or empty!{System.Environment.NewLine}=> 'Mark()' will return an empty string.", this);

            result = string.Empty;
         }
         else
         {
            if (string.IsNullOrEmpty(prefix))
            {
               if (Util.Constants.DEV_DEBUG)
                  Debug.LogWarning($"Parameter 'prefix' is null!{System.Environment.NewLine}=> Using an empty string as prefix.", this);

               _prefix = string.Empty;
            }

            if (string.IsNullOrEmpty(postfix))
            {
               if (Util.Constants.DEV_DEBUG)
                  Debug.LogWarning($"Parameter 'postfix' is null!{System.Environment.NewLine}=> Using an empty string as postfix.", this);

               _postfix = string.Empty;
            }

            if (unwantedWords == null || unwantedWords.Count == 0)
            {
               if (Util.Constants.DEV_DEBUG)
                  Debug.LogWarning($"Parameter 'unwantedWords' is null or empty!{System.Environment.NewLine}=> 'Mark()' will return the original string.", this);
            }
            else
            {
               result = unwantedWords.Where(unwantedWord => !string.IsNullOrEmpty(unwantedWord)).Aggregate(result, (current, unwantedWord) => current.Replace(unwantedWord, _prefix + unwantedWord + _postfix));
            }
         }

         return result;
      }

      /// <summary>Marks the text with a prefix and postfix.</summary>
      /// <param name="text">Text containing unwanted words</param>
      /// <param name="replace">Replace the bad words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found unwanted word (optional)</param>
      /// <param name="postfix">Postfix for every found unwanted word (optional)</param>
      /// <param name="mask">Active manager (default: Model.Enum.ManagerMask.All, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>Clean text</returns>
      public string Mark(string text, bool replace = false, string prefix = "<b><color=red>", string postfix = "</color></b>", Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         string result = text ?? string.Empty;

         if ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.BadWordManager.Instance.Mark(result, replace, prefix, postfix, sourceNames);

         if ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.DomainManager.Instance.Mark(result, replace, prefix, postfix, sourceNames);

         if ((mask & Model.Enum.ManagerMask.Capitalization) == Model.Enum.ManagerMask.Capitalization || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.CapitalizationManager.Instance.Mark(result, replace, prefix, postfix);

         if ((mask & Model.Enum.ManagerMask.Punctuation) == Model.Enum.ManagerMask.Punctuation || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.PunctuationManager.Instance.Mark(result, replace, prefix, postfix);

         return result;
      }

      /// <summary>Unmarks the text with a prefix and postfix.</summary>
      /// <param name="text">Text with marked unwanted words</param>
      /// <param name="prefix">Prefix for every found unwanted word (optional)</param>
      /// <param name="postfix">Postfix for every found unwanted word (optional)</param>
      /// <returns>Text with unmarked unwanted words</returns>
      public string Unmark(string text, string prefix = "<b><color=red>", string postfix = "</color></b>")
      {
         string result = text ?? string.Empty;

         //The different mangers all do the same.
         result = Manager.BadWordManager.Instance.Unmark(result, prefix, postfix);

         return result;
      }

      #endregion


      #region Private methods

      private IEnumerator containsAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            bool result = true;

            worker = new System.Threading.Thread(() => contains(out result, text, mask, sourceNames));
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

      private IEnumerator getAllAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            System.Collections.Generic.List<string> result = null;

            worker = new System.Threading.Thread(() => getAll(out result, text, mask, sourceNames));
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

      private IEnumerator replaceAllAsync(string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            string result = null;

            worker = new System.Threading.Thread(() => replaceAll(out result, text, mask, markOnly, prefix, postfix, sourceNames));
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

      private static void contains(out bool result, string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         result = ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All) && Manager.BadWordManager.Instance.Contains(text, sourceNames) ||
                  ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All) && Manager.DomainManager.Instance.Contains(text, sourceNames) ||
                  ((mask & Model.Enum.ManagerMask.Capitalization) == Model.Enum.ManagerMask.Capitalization || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All) && Manager.CapitalizationManager.Instance.Contains(text) ||
                  ((mask & Model.Enum.ManagerMask.Punctuation) == Model.Enum.ManagerMask.Punctuation || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All) && Manager.PunctuationManager.Instance.Contains(text);
      }

      private static void getAll(out System.Collections.Generic.List<string> result, string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, params string[] sourceNames)
      {
         Instance.getAllResult.Clear();

         if ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
         {
            Manager.BadWordManager.Instance.DisableOrdering = Instance.DisableOrdering;
            Instance.getAllResult.AddRange(Manager.BadWordManager.Instance.GetAll(text, sourceNames));
         }

         if ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
         {
            Manager.DomainManager.Instance.DisableOrdering = Instance.DisableOrdering;
            Instance.getAllResult.AddRange(Manager.DomainManager.Instance.GetAll(text, sourceNames));
         }

         if ((mask & Model.Enum.ManagerMask.Capitalization) == Model.Enum.ManagerMask.Capitalization || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
         {
            Manager.CapitalizationManager.Instance.DisableOrdering = Instance.DisableOrdering;
            Instance.getAllResult.AddRange(Manager.CapitalizationManager.Instance.GetAll(text));
         }

         if ((mask & Model.Enum.ManagerMask.Punctuation) == Model.Enum.ManagerMask.Punctuation || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
         {
            Manager.PunctuationManager.Instance.DisableOrdering = Instance.DisableOrdering;
            Instance.getAllResult.AddRange(Manager.PunctuationManager.Instance.GetAll(text));
         }

         result = Instance.DisableOrdering ? Instance.getAllResult : Instance.getAllResult.Distinct().OrderBy(x => x).ToList();
      }

      private static void replaceAll(out string result, string text, Model.Enum.ManagerMask mask = Model.Enum.ManagerMask.All, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
         result = text ?? string.Empty;

         if ((mask & Model.Enum.ManagerMask.BadWord) == Model.Enum.ManagerMask.BadWord || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.BadWordManager.Instance.ReplaceAll(result, markOnly, prefix, postfix, sourceNames);

         if ((mask & Model.Enum.ManagerMask.Domain) == Model.Enum.ManagerMask.Domain || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.DomainManager.Instance.ReplaceAll(result, markOnly, prefix, postfix, sourceNames);

         if ((mask & Model.Enum.ManagerMask.Capitalization) == Model.Enum.ManagerMask.Capitalization || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.CapitalizationManager.Instance.ReplaceAll(result, markOnly, prefix, postfix);

         if ((mask & Model.Enum.ManagerMask.Punctuation) == Model.Enum.ManagerMask.Punctuation || (mask & Model.Enum.ManagerMask.All) == Model.Enum.ManagerMask.All)
            result = Manager.PunctuationManager.Instance.ReplaceAll(result, markOnly, prefix, postfix);
      }

      #endregion


      #region Event-trigger methods

      private void onBWFReady()
      {
         if (!Util.Helper.isEditorMode)
            OnReady?.Invoke();

         OnBWFReady?.Invoke();
      }

      private void onContainsComplete(string text, bool result)
      {
         if (!Util.Helper.isEditorMode)
            OnContainsCompleted?.Invoke(text, result);

         OnContainsComplete?.Invoke(text, result);
      }

      private void onGetAllComplete(string text, System.Collections.Generic.List<string> badWords)
      {
         if (!Util.Helper.isEditorMode)
            OnGetAllCompleted?.Invoke(text, string.Join(";", badWords.ToArray()));

         OnGetAllComplete?.Invoke(text, badWords);
      }

      private void onReplaceAllComplete(string originalText, string cleanText)
      {
         if (!Util.Helper.isEditorMode)
            OnReplaceAllCompleted?.Invoke(originalText, cleanText);

         OnReplaceAllComplete?.Invoke(originalText, cleanText);
      }

      #endregion
   }
}
// © 2015-2021 crosstales LLC (https://www.crosstales.com)
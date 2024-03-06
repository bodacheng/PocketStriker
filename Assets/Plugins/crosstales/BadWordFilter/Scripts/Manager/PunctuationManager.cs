using UnityEngine;
using System.Collections;
using Crosstales.BWF.Filter;

namespace Crosstales.BWF.Manager
{
   /// <summary>Manager for excessive punctuation.</summary>
   [DisallowMultipleComponent]
   [HelpURL("https://www.crosstales.com/media/data/assets/badwordfilter/api/class_crosstales_1_1_b_w_f_1_1_manager_1_1_punctuation_manager.html")]
   public class PunctuationManager : Crosstales.BWF.Manager.BaseManager<PunctuationManager, PunctuationFilter>
   {
      #region Variables

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("PunctuationCharsNumber")] [Header("Specific Settings")] [Tooltip("Defines the number of allowed punctuation letters in a row (default: 3)."), SerializeField]
      private int punctuationCharsNumber = 3;


      [Header("Events")] public Crosstales.BWF.OnContainsCompleted OnContainsCompleted;
      public Crosstales.BWF.OnGetAllCompleted OnGetAllCompleted;
      public Crosstales.BWF.OnReplaceAllCompleted OnReplaceAllCompleted;

#if !UNITY_WSA && !UNITY_WEBGL
      private System.Threading.Thread worker;
#endif

      #endregion


      #region Properties

      /// <summary>Defines the number of allowed punctuation letters in a row (default: 3).</summary>
      public int PunctuationCharsNumber
      {
         get => filter?.CharacterNumber ?? punctuationCharsNumber;
         set => filter.CharacterNumber = punctuationCharsNumber = value < 1 ? 1 : value;
      }

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

      private void OnValidate()
      {
         if (punctuationCharsNumber < 1)
            punctuationCharsNumber = 1;

         if (punctuationCharsNumber != PunctuationCharsNumber)
            PunctuationCharsNumber = punctuationCharsNumber;
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
         filter = new PunctuationFilter(punctuationCharsNumber, DisableOrdering);
      }

      /// <summary>Searches for excessive punctuations in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <returns>True if a match was found</returns>
      public bool Contains(string text)
      {
         bool result = false;

         if (!string.IsNullOrEmpty(text) && filter != null)
            result = filter.Contains(text);

         return result;
      }

      /// <summary>Searches asynchronously for excessive punctuations in a text. Use the "OnContainsComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      public void ContainsAsync(string text)
      {
         StartCoroutine(containsAsync(text));
      }

      /// <summary>Searches for excessive punctuations in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <returns>List with all the matches</returns>
      public System.Collections.Generic.List<string> GetAll(string text)
      {
         System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();

         if (!string.IsNullOrEmpty(text))
            result = filter?.GetAll(text);

         return result;
      }

      /// <summary>Searches asynchronously for excessive punctuations in a text. Use the "OnGetAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      public void GetAllAsync(string text)
      {
         StartCoroutine(getAllAsync(text));
      }

      /// <summary>Searches and replaces all excessive punctuations in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found punctuation (optional)</param>
      /// <param name="postfix">Postfix for every found punctuation (optional)</param>
      /// <returns>Clean text</returns>
      public string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "")
      {
         string result = text;

         if (!string.IsNullOrEmpty(text))
            result = filter?.ReplaceAll(text, markOnly, prefix, postfix);


         return result;
      }

      /// <summary>Searches and replaces asynchronously all domains in a text. Use the "OnReplaceAllComplete"-callback to get the result.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found punctuation (optional)</param>
      /// <param name="postfix">Postfix for every found punctuation (optional)</param>
      public void ReplaceAllAsync(string text, bool markOnly = false, string prefix = "", string postfix = "")
      {
         StartCoroutine(replaceAllAsync(text, markOnly, prefix, postfix));
      }

      /// <summary>Marks the text with a prefix and postfix.</summary>
      /// <param name="text">Text containing excessive punctuations</param>
      /// <param name="replace">Replace the excessive punctuations (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found punctuation (default: bold and red, optional)</param>
      /// <param name="postfix">Postfix for every found punctuation (default: bold and red, optional)</param>
      /// <returns>Text with marked excessive punctuations</returns>
      public string Mark(string text, bool replace = false, string prefix = "<b><color=red>", string postfix = "</color></b>")
      {
         string result = text;

         if (!string.IsNullOrEmpty(text))
            result = filter?.Mark(text, replace, prefix, postfix);


         return result;
      }

      #endregion


      #region Private methods

      private IEnumerator containsAsync(string text)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            bool result = true;

            worker = new System.Threading.Thread(() => result = Contains(text));
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

      private IEnumerator getAllAsync(string text)
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            System.Collections.Generic.List<string> result = null;

            worker = new System.Threading.Thread(() => result = GetAll(text));
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

      private IEnumerator replaceAllAsync(string text, bool markOnly = false, string prefix = "", string postfix = "")
      {
#if !UNITY_WSA && !UNITY_WEBGL
         if (worker?.IsAlive != true)
         {
            string result = null;

            worker = new System.Threading.Thread(() => result = ReplaceAll(text, markOnly, prefix, postfix));
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
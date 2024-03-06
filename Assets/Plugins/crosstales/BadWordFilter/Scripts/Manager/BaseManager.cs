using UnityEngine;

namespace Crosstales.BWF.Manager
{
   /// <summary>Base class for all managers.</summary>
   [ExecuteInEditMode]
   public abstract class BaseManager<S, T> : Crosstales.Common.Util.Singleton<S> where S : Crosstales.Common.Util.Singleton<S> where T : Crosstales.BWF.Filter.BaseFilter
   {
      #region Variables

      [Tooltip("Disables the ordering of the 'GetAll'-method (prevent possible memory garbage)."), SerializeField]
      private bool disableOrdering;

      protected T filter;

      #endregion


      #region Properties

      public bool DisableOrdering
      {
         get => disableOrdering;
         set
         {
            //Debug.Log($"SET DisableOrdering: {value} - {filter}");

            if (filter != null)
            {
               disableOrdering = value;
               filter.DisableOrdering = disableOrdering;
            }
         }
      }

      /// <summary>Checks the readiness status of the manager.</summary>
      /// <returns>True if the manager is ready.</returns>
      public bool isReady => filter != null && filter.isReady;

      protected abstract OnContainsCompleted onContainsCompleted { get; }

      protected abstract OnGetAllCompleted onGetAllCompleted { get; }

      protected abstract OnReplaceAllCompleted onReplaceAllCompleted { get; }

      #endregion


      #region Events

      /// <summary>An event triggered whenever the "Contains"-operation is completed.</summary>
      public event ContainsComplete OnContainsComplete;

      /// <summary>An event triggered whenever the "GetAll"-operation is completed.</summary>
      public event GetAllComplete OnGetAllComplete;

      /// <summary>An event triggered whenever the "ReplaceAll"-operation is completed.</summary>
      public event ReplaceAllComplete OnReplaceAllComplete;

      #endregion


      #region MonoBehaviour methods

      private void Start()
      {
         //do nothing, just allow to enable/disable the script
      }

      #endregion


      #region Public methods

      /// <summary>Unmarks the text with a prefix and postfix.</summary>
      /// <param name="text">Text with marked bad words</param>
      /// <param name="prefix">Prefix for every found bad word (default: bold and red, optional)</param>
      /// <param name="postfix">Postfix for every found bad word (default: bold and red, optional)</param>
      /// <returns>Text with unmarked bad words</returns>
      public string Unmark(string text, string prefix = "<b><color=red>", string postfix = "</color></b>")
      {
         string result = text;

         if (!string.IsNullOrEmpty(text))
         {
            if (filter != null)
            {
               result = filter.Unmark(text, prefix, postfix);
            }
         }

         return result;
      }

      #endregion


      #region Event-trigger methods

      protected void onContainsComplete(string text, bool result)
      {
         if (!Crosstales.BWF.Util.Helper.isEditorMode)
            onContainsCompleted?.Invoke(text, result);

         OnContainsComplete?.Invoke(text, result);
      }

      protected void onGetAllComplete(string text, System.Collections.Generic.List<string> badWords)
      {
         if (!Crosstales.BWF.Util.Helper.isEditorMode)
            onGetAllCompleted?.Invoke(text, string.Join(";", badWords.ToArray()));

         OnGetAllComplete?.Invoke(text, badWords);
      }

      protected void onReplaceAllComplete(string originalText, string cleanText)
      {
         if (!Crosstales.BWF.Util.Helper.isEditorMode)
            onReplaceAllCompleted?.Invoke(originalText, cleanText);

         OnReplaceAllComplete?.Invoke(originalText, cleanText);
      }

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
using UnityEngine;
using System.Linq;
using Crosstales.BWF.Data;
using Crosstales.BWF.Util;

namespace Crosstales.BWF.Filter
{
   /// <summary>Base class for all filters.</summary>
   public abstract class BaseFilter : IFilter
   {
      #region Variables

      public bool DisableOrdering;

      protected readonly System.Collections.Generic.Dictionary<string, Source> sources = new System.Collections.Generic.Dictionary<string, Source>();

      protected readonly System.Collections.Generic.List<string> getAllResult = new System.Collections.Generic.List<string>();

      #endregion


      #region Constructor

      /// <summary>Instantiate the class.</summary>
      /// <param name="disableOrdering">Disables the ordering of the 'GetAll'-method (prevent possible memory garbage).</param>
      public BaseFilter(bool disableOrdering)
      {
         DisableOrdering = disableOrdering;
      }

      #endregion


      #region Implemented methods

      public virtual System.Collections.Generic.List<Source> Sources
      {
         get
         {
            System.Collections.Generic.List<Source> result = new System.Collections.Generic.List<Source>();

            if (isReady)
            {
               result = sources.OrderBy(x => x.Key).Select(y => y.Value).ToList();
            }
            else
            {
               logFilterNotReady();
            }

            return result;
         }
      }

      public abstract bool isReady { get; }

      public abstract bool Contains(string text, params string[] sourceNames);

      public abstract System.Collections.Generic.List<string> GetAll(string text, params string[] sourceNames);

      public abstract string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames);

      public virtual string Unmark(string text, string prefix = "<b><color=red>", string postfix = "</color></b>")
      {
         string result = text;

         string _prefix = prefix;
         string _postfix = postfix;

         if (string.IsNullOrEmpty(text))
         {
            if (Constants.DEV_DEBUG)
               Debug.LogWarning($"Parameter 'text' is null or empty!{System.Environment.NewLine}=> 'Unmark()' will return an empty string.");

            result = string.Empty;
         }
         else
         {
            if (string.IsNullOrEmpty(prefix))
            {
               if (Constants.DEV_DEBUG)
                  Debug.LogWarning($"Parameter 'prefix' is null!{System.Environment.NewLine}=> Using an empty string as prefix.");

               _prefix = string.Empty;
            }

            if (string.IsNullOrEmpty(postfix))
            {
               if (Constants.DEV_DEBUG)
                  Debug.LogWarning($"Parameter 'postfix' is null!{System.Environment.NewLine}=> Using an empty string as postfix.");

               _postfix = string.Empty;
            }

            result = result.Replace(_prefix, string.Empty);
            result = result.Replace(_postfix, string.Empty);
         }

         return result;
      }

      public virtual string Mark(string text, bool replace = false, string prefix = "<b><color=red>", string postfix = "</color></b>", params string[] sourceNames)
      {
         return ReplaceAll(text, !replace, prefix, postfix, sourceNames);
      }

      #endregion


      #region Protected methods

      protected static void logFilterNotReady()
      {
         Debug.LogWarning("Filter is not ready - please wait until 'isReady' returns true.");
      }

      protected static void logResourceNotFound(string res)
      {
         if (Constants.DEV_DEBUG)
            Debug.LogWarning($"Resource not found: '{res}'{System.Environment.NewLine}Did you call the method with the correct resource name?");
      }

      protected static void logContains()
      {
         if (Constants.DEV_DEBUG)
            Debug.LogWarning($"Parameter 'text' is null or empty!{System.Environment.NewLine}=> 'Contains()' will return 'false'.");
      }

      protected static void logGetAll()
      {
         if (Constants.DEV_DEBUG)
            Debug.LogWarning($"Parameter 'text' is null or empty!{System.Environment.NewLine}=> 'GetAll()' will return an empty list.");
      }

      protected static void logReplaceAll()
      {
         if (Constants.DEV_DEBUG)
            Debug.LogWarning($"Parameter 'text' is null or empty!{System.Environment.NewLine}=> 'ReplaceAll()' will return an empty string.");
      }

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
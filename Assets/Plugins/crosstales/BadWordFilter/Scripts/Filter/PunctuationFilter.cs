using UnityEngine;
using System.Linq;
using Crosstales.BWF.Util;

namespace Crosstales.BWF.Filter
{
   /// <summary>Filter for excessive punctuation. The class can also replace all punctuations inside a string.</summary>
   public class PunctuationFilter : BaseFilter
   {
      #region Variables

      /// <summary>RegEx to find excessive punctuation.</summary>
      public System.Text.RegularExpressions.Regex RegularExpression { get; private set; }

      private int characterNumber;

      #endregion


      #region Properties

      /// <summary>Defines the number of allowed punctuations in a row.</summary>
      public int CharacterNumber
      {
         get => characterNumber;
         set
         {
            characterNumber = value < 1 ? 1 : value;

            RegularExpression = new System.Text.RegularExpressions.Regex($@"[?!,.;:-]{{{characterNumber + 1},}}", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
         }
      }

      /// <summary>Checks the readiness status of the filter.</summary>
      /// <returns>True if the filter is ready.</returns>
      public override bool isReady => true; //is always ready

      #endregion


      #region Constructor

      /// <summary>Instantiate the class.</summary>
      /// <param name="punctuationCharacterNumber">Defines the number of allowed punctuations in a row (default: 3, optional).</param>
      /// <param name="disableOrdering">Disables the ordering of the 'GetAll'-method (default: false, optional).</param>
      public PunctuationFilter(int punctuationCharacterNumber = 3, bool disableOrdering = false /*, string markPrefix, string markPostfix */) : base(disableOrdering)
      {
         CharacterNumber = punctuationCharacterNumber;
      }

      #endregion


      #region Implemented methods

      public override bool Contains(string text, params string[] sourceNames) //sources are ignored
      {
         bool result = false;

         if (string.IsNullOrEmpty(text))
         {
            logContains();
         }
         else
         {
            result = RegularExpression.Match(text).Success;
         }

         return result;
      }

      public override System.Collections.Generic.List<string> GetAll(string text, params string[] sourceNames) //sources are ignored
      {
         getAllResult.Clear();

         if (string.IsNullOrEmpty(text))
         {
            logGetAll();
         }
         else
         {
            System.Text.RegularExpressions.MatchCollection matches = RegularExpression.Matches(text);

            foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
            {
               if (Constants.DEV_DEBUG)
                  Debug.Log($"Test string contains an excessive punctuation: '{capture.Value}'");

               if (!getAllResult.Contains(capture.Value))
               {
                  getAllResult.Add(capture.Value);
               }
            }
         }

         return DisableOrdering ? getAllResult : getAllResult.Distinct().OrderBy(x => x).ToList();
      }


      public override string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames) //sources are ignored
      {
         string result = text;

         if (string.IsNullOrEmpty(text))
         {
            logReplaceAll();

            result = string.Empty;
         }
         else
         {
            System.Text.RegularExpressions.MatchCollection matches = RegularExpression.Matches(text);

            foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
            {
               if (Constants.DEV_DEBUG)
                  Debug.Log($"Test string contains an excessive punctuation: '{capture.Value}'");

               result = result.Replace(capture.Value, markOnly ? prefix + capture.Value + postfix : prefix + capture.Value.Substring(0, characterNumber) + postfix);
            }
         }

         return result;
      }

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
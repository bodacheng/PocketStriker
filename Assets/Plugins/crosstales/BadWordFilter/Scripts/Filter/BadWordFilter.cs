using UnityEngine;
using System.Linq;
using Crosstales.BWF.Provider;
using Crosstales.BWF.Data;
using Crosstales.BWF.Util;
using Crosstales.BWF.Model.Enum;

namespace Crosstales.BWF.Filter
{
   /// <summary>Filter for bad words. The class can also replace all bad words inside a string.</summary>
   public class BadWordFilter : BaseFilter
   {
      #region Variables

      /// <summary>Replace characters for bad words.</summary>
      public string ReplaceCharacters;

      /// <summary>Replace mode operations on the input string.</summary>
      public ReplaceMode Mode;

      /// <summary>Remove unnecessary spaces between letters in the input string.</summary>
      public bool RemoveSpaces;

      /// <summary>Use simple detection algorithm.</summary>
      public bool SimpleCheck;

      private readonly System.Collections.Generic.List<BadWordProvider> tempBadWordProviderLTR; //left-to-right
      private readonly System.Collections.Generic.List<BadWordProvider> tempBadWordProviderRTL; //right-to-left

      private readonly System.Collections.Generic.Dictionary<string, System.Text.RegularExpressions.Regex> exactBadwordsRegex = new System.Collections.Generic.Dictionary<string, System.Text.RegularExpressions.Regex>(30);
      private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Text.RegularExpressions.Regex>> debugExactBadwordsRegex = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Text.RegularExpressions.Regex>>(30);
      private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> simpleBadwords = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(30);

      private bool ready;
      private bool readyFirstime;

      private System.Collections.Generic.List<BadWordProvider> badWordProviderLTR = new System.Collections.Generic.List<BadWordProvider>(); //left-to-right
      private System.Collections.Generic.List<BadWordProvider> badWordProviderRTL = new System.Collections.Generic.List<BadWordProvider>(); //right-to-left

      #endregion


      #region Properties

      /// <summary>List of all left-to-right providers.</summary>
      /// <returns>All left-to-right providers.</returns>
      public System.Collections.Generic.List<BadWordProvider> BadWordProviderLTR
      {
         get => badWordProviderLTR;
         set
         {
            badWordProviderLTR = value;
            if (badWordProviderLTR?.Count > 0)
            {
               foreach (BadWordProvider bp in badWordProviderLTR)
               {
                  if (bp != null)
                  {
                     if (Config.DEBUG_BADWORDS)
                     {
                        debugExactBadwordsRegex.CTAddRange(bp.DebugExactBadwordsRegex);
                     }
                     else
                     {
                        exactBadwordsRegex.CTAddRange(bp.ExactBadwordsRegex);
                     }

                     simpleBadwords.CTAddRange(bp.SimpleBadwords);
                  }
                  else
                  {
                     if (!Helper.isEditorMode)
                        Debug.LogError("A LTR-BadWordProvider is null!");
                  }
               }
            }
            else
            {
               badWordProviderLTR = new System.Collections.Generic.List<BadWordProvider>();
            }
         }
      }

      /// <summary>List of all right-to-left providers.</summary>
      /// <returns>All right-to-left providers.</returns>
      public System.Collections.Generic.List<BadWordProvider> BadWordProviderRTL
      {
         get => badWordProviderRTL;
         set
         {
            badWordProviderRTL = value;
            if (badWordProviderRTL?.Count > 0)
            {
               foreach (BadWordProvider bp in badWordProviderRTL)
               {
                  if (bp != null)
                  {
                     if (Config.DEBUG_BADWORDS)
                     {
                        debugExactBadwordsRegex.CTAddRange(bp.DebugExactBadwordsRegex);
                     }
                     else
                     {
                        exactBadwordsRegex.CTAddRange(bp.ExactBadwordsRegex);
                     }

                     simpleBadwords.CTAddRange(bp.SimpleBadwords);
                  }
                  else
                  {
                     if (!Helper.isEditorMode)
                        Debug.LogError("A RTL-BadWordProvider is null!");
                  }
               }
            }
            else
            {
               badWordProviderRTL = new System.Collections.Generic.List<BadWordProvider>();
            }
         }
      }

      /// <summary>Checks the readiness status of the filter.</summary>
      /// <returns>True if the filter is ready.</returns>
      public override bool isReady
      {
         get
         {
            bool result = true;

            if (!ready)
            {
               if (tempBadWordProviderLTR?.Any(bp => bp != null && !bp.isReady) == true)
                  result = false;

               if (result)
               {
                  if (tempBadWordProviderRTL?.Any(bp => bp != null && !bp.isReady) == true)
                  {
                     result = false;
                  }
               }

               if (!readyFirstime && result)
               {
                  BadWordProviderLTR = tempBadWordProviderLTR;
                  BadWordProviderRTL = tempBadWordProviderRTL;

                  if (BadWordProviderLTR != null)
                  {
                     foreach (Source src in from bpLTR in BadWordProviderLTR where bpLTR != null from src in bpLTR.Sources where src != null where !sources.ContainsKey(src.SourceName) select src)
                     {
                        sources.Add(src.SourceName, src);
                     }
                  }

                  if (BadWordProviderRTL != null)
                  {
                     foreach (Source src in from bpRTL in BadWordProviderRTL where bpRTL != null from src in bpRTL.Sources where src != null where !sources.ContainsKey(src.SourceName) select src)
                     {
                        sources.Add(src.SourceName, src);
                     }
                  }

                  readyFirstime = true;
               }
            }

            ready = result;

            return result;
         }
      }

      #endregion


      #region Constructor

      /// <summary>Instantiate the class.</summary>
      /// <param name="badWordProviderLTR">List of all left-to-right providers.</param>
      /// <param name="badWordProviderRTL">List of all right-to-left providers.</param>
      /// <param name="replaceCharacters">Replace characters for bad words (default: *, optional).</param>
      /// <param name="mode">Replace mode operations on the input string (default: Default, optional).</param>
      /// <param name="simpleCheck">Use simple detection algorithm (default: false, optional).</param>
      /// <param name="removeSpaces">Replace Leet speak in the input string (default: false, optional).</param>
      /// <param name="disableOrdering">Disables the ordering of the 'GetAll'-method (default: false, optional).</param>
      public BadWordFilter(System.Collections.Generic.List<BadWordProvider> badWordProviderLTR, System.Collections.Generic.List<BadWordProvider> badWordProviderRTL, string replaceCharacters = "*", ReplaceMode mode = ReplaceMode.Default, bool simpleCheck = false, bool removeSpaces = false, bool disableOrdering = false /*, string markPrefix, string markPostfix*/) : base(disableOrdering)
      {
         tempBadWordProviderLTR = badWordProviderLTR;
         tempBadWordProviderRTL = badWordProviderRTL;

         ReplaceCharacters = replaceCharacters;
         Mode = mode;
         SimpleCheck = simpleCheck;
         RemoveSpaces = removeSpaces;
         DisableOrdering = disableOrdering;
      }

      #endregion


      #region Implemented methods

      public override bool Contains(string text, params string[] sourceNames)
      {
         bool result = false;

         if (isReady)
         {
            if (string.IsNullOrEmpty(text))
            {
               logContains();
            }
            else
            {
               string _text = replaceText(text);
               System.Text.RegularExpressions.Match match;

               #region DEBUG

               if (Config.DEBUG_BADWORDS)
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     if (SimpleCheck)
                     {
                        foreach (System.Collections.Generic.List<string> words in simpleBadwords.Values)
                        {
                           result = words.Any(simpleWord => _text.CTContains(simpleWord));

                           if (result)
                           {
                              Debug.Log("Test string contains a bad word.");
                              break;
                           }
                        }
                     }
                     else
                     {
                        foreach (System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordRegexes in debugExactBadwordsRegex.Values)
                        {
                           foreach (System.Text.RegularExpressions.Regex badWordRegex in badWordRegexes)
                           {
                              match = badWordRegex.Match(_text);
                              if (match.Success)
                              {
                                 Debug.Log($"Test string contains a bad word: '{match.Value}' detected by regex '{badWordRegex}'");
                                 result = true;
                                 break;
                              }
                           }
                        }
                     }
                  }
                  else
                  {
                     for (int ii = 0; ii < sourceNames.Length && !result; ii++)
                     {
                        if (SimpleCheck)
                        {
                           if (simpleBadwords.TryGetValue(sourceNames[ii], out System.Collections.Generic.List<string> words))
                           {
                              result = words.Any(simpleWord => _text.CTContains(simpleWord));

                              if (result)
                              {
                                 Debug.Log($"Test string contains a bad word from source '{sourceNames[ii]}'");
                                 break;
                              }
                           }
                           else
                           {
                              logResourceNotFound(sourceNames[ii]);
                           }
                        }
                        else
                        {
                           if (debugExactBadwordsRegex.TryGetValue(sourceNames[ii], out System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordRegexes))
                           {
                              foreach (System.Text.RegularExpressions.Regex badWordRegex in badWordRegexes)
                              {
                                 match = badWordRegex.Match(_text);
                                 if (match.Success)
                                 {
                                    Debug.Log($"Test string contains a bad word: '{match.Value}' detected by regex '{badWordRegex}' from source '{sourceNames[ii]}'");
                                    result = true;
                                    break;
                                 }
                              }
                           }
                           else
                           {
                              logResourceNotFound(sourceNames[ii]);
                           }
                        }
                     }
                  }
               }

               #endregion

               else
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     if (SimpleCheck)
                     {
                        if (simpleBadwords.Values.Any(words => words.Any(simpleWord => _text.CTContains(simpleWord))))
                           result = true;
                     }
                     else
                     {
                        if (exactBadwordsRegex.Values.Any(badWordRegex => badWordRegex.Match(_text).Success))
                           result = true;
                     }
                  }
                  else
                  {
                     foreach (string badWordsResource in sourceNames)
                     {
                        if (SimpleCheck)
                        {
                           if (simpleBadwords.TryGetValue(badWordsResource, out System.Collections.Generic.List<string> words))
                           {
                              if (words.Any(simpleWord => _text.CTContains(simpleWord)))
                              {
                                 result = true;
                                 break;
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                        else
                        {
                           if (exactBadwordsRegex.TryGetValue(badWordsResource, out System.Text.RegularExpressions.Regex badWordRegex))
                           {
                              match = badWordRegex.Match(_text);
                              if (match.Success)
                              {
                                 result = true;
                                 break;
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                     }
                  }
               }
            }
         }
         else
         {
            logFilterNotReady();
         }

         return result;
      }

      public override System.Collections.Generic.List<string> GetAll(string text, params string[] sourceNames)
      {
         getAllResult.Clear();

         if (isReady)
         {
            if (string.IsNullOrEmpty(text))
            {
               logGetAll();
            }
            else
            {
               string _text = replaceText(text);

               #region DEBUG

               if (Config.DEBUG_BADWORDS)
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     if (SimpleCheck)
                     {
                        foreach (string simpleWord in from words in simpleBadwords.Values from simpleWord in words where _text.CTContains(simpleWord) select simpleWord)
                        {
                           Debug.Log($"Test string contains a bad word detected by word '{simpleWord}'");

                           if (!getAllResult.Contains(simpleWord))
                              getAllResult.Add(simpleWord);
                        }
                     }
                     else
                     {
                        foreach (System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordsResources in debugExactBadwordsRegex.Values)
                        {
                           foreach (System.Text.RegularExpressions.Regex badWordsResource in badWordsResources)
                           {
                              System.Text.RegularExpressions.MatchCollection matches = badWordsResource.Matches(_text);

                              foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                              {
                                 Debug.Log($"Test string contains a bad word: '{capture.Value}' detected by regex '{badWordsResource}'");

                                 if (!getAllResult.Contains(capture.Value))
                                    getAllResult.Add(capture.Value);
                              }
                           }
                        }
                     }
                  }
                  else
                  {
                     foreach (string badWordsResource in sourceNames)
                     {
                        if (SimpleCheck)
                        {
                           if (simpleBadwords.TryGetValue(badWordsResource, out System.Collections.Generic.List<string> words))
                           {
                              foreach (string simpleWord in words.Where(simpleWord => _text.CTContains(simpleWord)))
                              {
                                 Debug.Log($"Test string contains a bad word detected by word '{simpleWord}' from source '{badWordsResource}'");

                                 if (!getAllResult.Contains(simpleWord))
                                    getAllResult.Add(simpleWord);
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                        else
                        {
                           if (debugExactBadwordsRegex.TryGetValue(badWordsResource, out System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordRegexes))
                           {
                              foreach (System.Text.RegularExpressions.Regex badWordRegex in badWordRegexes)
                              {
                                 System.Text.RegularExpressions.MatchCollection matches = badWordRegex.Matches(_text);

                                 foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                                 {
                                    Debug.Log($"Test string contains a bad word: '{capture.Value}' detected by regex '{badWordRegex}' from source '{badWordsResource}'");

                                    if (!getAllResult.Contains(capture.Value))
                                       getAllResult.Add(capture.Value);
                                 }
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                     }
                  }
               }

               #endregion

               else
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     if (SimpleCheck)
                     {
                        foreach (string simpleWord in from words in simpleBadwords.Values from simpleWord in words where _text.CTContains(simpleWord) where !getAllResult.Contains(simpleWord) select simpleWord)
                        {
                           getAllResult.Add(simpleWord);
                        }
                     }
                     else
                     {
                        foreach (System.Text.RegularExpressions.Capture capture in from badWordsResource in exactBadwordsRegex.Values select badWordsResource.Matches(_text) into matches from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures where !getAllResult.Contains(capture.Value) select capture)
                        {
                           getAllResult.Add(capture.Value);
                        }
                     }
                  }
                  else
                  {
                     foreach (string badWordsResource in sourceNames)
                     {
                        if (SimpleCheck)
                        {
                           if (simpleBadwords.TryGetValue(badWordsResource, out System.Collections.Generic.List<string> words))
                           {
                              foreach (string simpleWord in words.Where(simpleWord => _text.CTContains(simpleWord)).Where(simpleWord => !getAllResult.Contains(simpleWord)))
                              {
                                 getAllResult.Add(simpleWord);
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                        else
                        {
                           if (exactBadwordsRegex.TryGetValue(badWordsResource, out System.Text.RegularExpressions.Regex badWordRegex))
                           {
                              System.Text.RegularExpressions.MatchCollection matches = badWordRegex.Matches(_text);

                              foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures where !getAllResult.Contains(capture.Value) select capture)
                              {
                                 getAllResult.Add(capture.Value);
                              }
                           }
                           else
                           {
                              logResourceNotFound(badWordsResource);
                           }
                        }
                     }
                  }
               }
            }
         }
         else
         {
            logFilterNotReady();
         }

         //Debug.Log("GETALL: " + DisableOrdering);

         return DisableOrdering ? getAllResult : getAllResult.Distinct().OrderBy(x => x).ToList();
      }

      public override string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames)
      {
         string result = string.Empty;
         bool hasBadWords = false;

         if (isReady)
         {
            if (string.IsNullOrEmpty(text))
            {
               logReplaceAll();
            }
            else
            {
               string _text = result = replaceText(text);

               if (SimpleCheck)
               {
                  foreach (string badword in GetAll(_text, sourceNames))
                  {
                     _text = System.Text.RegularExpressions.Regex.Replace(_text, badword, Helper.CreateString(ReplaceCharacters, badword.Length), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                     hasBadWords = true;
                  }

                  result = _text;
               }

               #region DEBUG

               else if (Config.DEBUG_BADWORDS)
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     foreach (System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordsResources in debugExactBadwordsRegex.Values)
                     {
                        foreach (System.Text.RegularExpressions.Regex badWordsResource in badWordsResources)
                        {
                           System.Text.RegularExpressions.MatchCollection matches = badWordsResource.Matches(_text);

                           foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                           {
                              Debug.Log($"Test string contains a bad word: '{capture.Value}' detected by regex '{badWordsResource}'");

                              result = replaceCapture(result, capture, markOnly, prefix, postfix, result.Length - _text.Length);

                              hasBadWords = true;
                           }
                        }
                     }
                  }
                  else
                  {
                     foreach (string badWordsResource in sourceNames)
                     {
                        if (debugExactBadwordsRegex.TryGetValue(badWordsResource, out System.Collections.Generic.List<System.Text.RegularExpressions.Regex> badWordRegexes))
                        {
                           foreach (System.Text.RegularExpressions.Regex badWordRegex in badWordRegexes)
                           {
                              System.Text.RegularExpressions.MatchCollection matches = badWordRegex.Matches(_text);

                              foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                              {
                                 Debug.Log($"Test string contains a bad word: '{capture.Value}' detected by regex '{badWordRegex}'' from source '{badWordsResource}'");

                                 result = replaceCapture(result, capture, markOnly, prefix, postfix, result.Length - _text.Length);

                                 hasBadWords = true;
                              }
                           }
                        }
                        else
                        {
                           logResourceNotFound(badWordsResource);
                        }
                     }
                  }
               }

               #endregion

               else
               {
                  if (sourceNames == null || sourceNames.Length == 0)
                  {
                     foreach (System.Text.RegularExpressions.Capture capture in from badWordsResource in exactBadwordsRegex.Values select badWordsResource.Matches(_text) into matches from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                     {
                        result = replaceCapture(result, capture, markOnly, prefix, postfix, result.Length - _text.Length);

                        hasBadWords = true;
                     }
                  }
                  else
                  {
                     foreach (string badWordsResource in sourceNames)
                     {
                        if (exactBadwordsRegex.TryGetValue(badWordsResource, out System.Text.RegularExpressions.Regex badWordRegex))
                        {
                           System.Text.RegularExpressions.MatchCollection matches = badWordRegex.Matches(_text);

                           foreach (System.Text.RegularExpressions.Capture capture in from System.Text.RegularExpressions.Match match in matches from System.Text.RegularExpressions.Capture capture in match.Captures select capture)
                           {
                              result = replaceCapture(result, capture, markOnly, prefix, postfix, result.Length - _text.Length);

                              hasBadWords = true;
                           }
                        }
                        else
                        {
                           logResourceNotFound(badWordsResource);
                        }
                     }
                  }
               }
            }
         }
         else
         {
            logFilterNotReady();
         }

         return hasBadWords ? result : text;
      }

      #endregion


      #region Private methods

      private string replaceCapture(string text, System.Text.RegularExpressions.Capture capture, bool markOnly, string prefix, string postfix, int offset)
      {
         System.Text.StringBuilder sb = new System.Text.StringBuilder(text);

         string replacement = markOnly ? prefix + capture.Value + postfix : prefix + Helper.CreateString(ReplaceCharacters, capture.Value.Length) + postfix;

         sb.Remove(capture.Index + offset, capture.Value.Length);
         sb.Insert(capture.Index + offset, replacement);

         return sb.ToString();
      }

      protected string replaceText(string input)
      {
         string result = input;

         if (RemoveSpaces)
            result = replaceSpacesBetweenLetters(result);

         switch (Mode)
         {
            case ReplaceMode.LeetSpeak:
               result = replaceLeetToText(result);
               break;
            case ReplaceMode.LeetSpeakAdvanced:
               result = replaceLeetAdvancedToText(result);
               result = replaceLeetToText(result);
               break;
            case ReplaceMode.NonLettersOrDigits:
               result = replaceNonLettersOrDigits(result);
               break;
         }

         return result;
      }

      private static string replaceNonLettersOrDigits(string input)
      {
         char[] arr = input.ToCharArray();

         arr = System.Array.FindAll(arr, c => char.IsLetterOrDigit(c)
                                              || char.IsWhiteSpace(c)
                                              || c == ','
                                              || c == '?'
                                              || c == '!'
                                              || c == '-'
                                              || c == ';'
                                              || c == ':'
                                              || c == '"'
                                              || c == '\''
                                              || c == '.');

         //Debug.Log(new string(arr));
         return new string(arr);
      }

      private static string replaceSpacesBetweenLetters(string text, int maxTextLength = 1)
      {
         if (string.IsNullOrEmpty(text))
            return text;

         string[] textArray = text.Split(new[] { " " }, System.StringSplitOptions.RemoveEmptyEntries);

         System.Text.StringBuilder sb = new System.Text.StringBuilder();

         for (int ii = 0; ii < textArray.Length; ii++)
         {
            string currentText = textArray[ii];

            if (currentText.Length <= maxTextLength)
            {
               sb.Append(currentText);

               for (int xx = ii + 1; xx < textArray.Length; xx++)
               {
                  string nextText = textArray[xx];

                  if (nextText.Length <= maxTextLength)
                  {
                     ii = xx;
                     sb.Append(nextText);
                  }
                  else
                  {
                     break;
                  }
               }
            }
            else
            {
               sb.Append(currentText);
            }

            if (ii < textArray.Length - 1)
               sb.Append(" ");
         }

         //Debug.Log(sb.ToString());
         return sb.ToString();
      }

      private static string replaceLeetToText(string input)
      {
         if (string.IsNullOrEmpty(input))
            return input;

         string result = input;

         // A
         result = result.Replace("@", "a");
         result = result.Replace("4", "a");
         result = result.Replace("^", "a");

         // B
         result = result.Replace("8", "b");

         // C
         result = result.Replace("©", "c");
         result = result.Replace('¢', 'c');

         // D

         // E
         result = result.Replace("€", "e");
         result = result.Replace("3", "e");
         result = result.Replace("£", "e");

         // F
         result = result.Replace("ƒ", "f");

         // G
         result = result.Replace("6", "g");
         result = result.Replace("9", "g");

         // H
         result = result.Replace("#", "h");

         // I
         result = result.Replace("1", "i");
         result = result.Replace("!", "i");
         result = result.Replace("|", "i");

         // J

         // K

         // L

         // M

         // N

         // O
         result = result.Replace("0", "o");

         // P

         // Q

         // R
         result = result.Replace("2", "r");
         result = result.Replace("®", "r");

         // S
         result = result.Replace("$", "s");
         result = result.Replace("5", "s");
         result = result.Replace("§", "s");

         // T
         result = result.Replace("7", "t");
         result = result.Replace("+", "t");
         result = result.Replace("†", "t");

         // U

         // V

         // W

         // X

         // Y
         result = result.Replace("¥", "y");

         // Z

         return result;
      }

      private static string replaceLeetAdvancedToText(string input)
      {
         if (string.IsNullOrEmpty(input))
            return input;

         string result = input;


         // B

         // C

         // D

         // E

         // F

         // G

         // H
         result = result.Replace("|-|", "h");
         result = result.Replace("}{", "h");
         result = result.Replace("]-[", "h");
         result = result.Replace("/-/", "h");
         result = result.Replace(")-(", "h");

         // I
         result = result.Replace("][", "i");

         // J

         // K
         result = result.Replace("|<", "k");
         result = result.Replace("|{", "k");
         result = result.Replace("|(", "k");

         // L
         result = result.Replace("|_", "l");
         result = result.Replace("][_", "l");

         // M
         result = result.Replace("/\\/\\", "m");
         result = result.Replace("/v\\", "m");
         result = result.Replace("|V|", "m");
         result = result.Replace("]V[", "m");
         result = result.Replace("|\\/|", "m");

         // N
         result = result.Replace("|\\|", "n");
         result = result.Replace("/\\/", "n");
         result = result.Replace("/V", "n");

         // O
         result = result.Replace("()", "o");

         // P
         result = result.Replace("|°", "p");
         result = result.Replace("|>", "p");

         // Q

         // R

         // S

         // T
         //result = result.Replace ("']['", "t");

         // U
         result = result.Replace("µ", "u");
         result = result.Replace("|_|", "u");

         // W
         result = result.Replace("\\/\\/", "w");

         // V
         result = result.Replace("\\/", "v");

         // X
         result = result.Replace("><", "x");
         result = result.Replace(")(", "x");

         // Y

         // Z

         // A
         result = result.Replace("/\\", "a");
         result = result.Replace("/-\\", "a");

         //Debug.Log("RESULT: " + result);
         return result;
      }
/*
        protected string replaceTextToLeet(string input, bool obvious = true)
        {
            string result = input;

            if (ReplaceLeetSpeak && !string.IsNullOrEmpty(input))
            {
                if (obvious)
                {
                    // I
                    //result = result.Replace("i", "!");

                    // S
                    result = result.Replace("s", "$");
                }
                else
                {
                    // A
                    result = result.Replace("a", "@");
                    //result = result.Replace("4", "a");
                    //result = result.Replace("^", "a");

                    // B
                    result = result.Replace("b", "8");

                    // C
                    //result = result.Replace("©", "c");
                    //result = result.Replace('¢', 'c');

                    // D

                    // E
                    //result = result.Replace("€", "e");
                    result = result.Replace("e", "3");
                    //result = result.Replace("£", "e");

                    // F
                    //result = result.Replace("ƒ", "f");

                    // G
                    //result = result.Replace("6", "g");
                    result = result.Replace("g", "9");

                    // H
                    //result = result.Replace("#", "h");
                    //result = result.Replace ("|-|", "h");
                    //result = result.Replace ("}{", "h");
                    //result = result.Replace ("]-[", "h");
                    //result = result.Replace ("/-/", "h");
                    //result = result.Replace (")-(", "h");

                    // I
                    result = result.Replace("i", "1");
                    //result = result.Replace("i", "!");
                    //result = result.Replace("|", "i");
                    //result = result.Replace ("][", "i");

                    // J

                    // K
                    //result = result.Replace ("|<", "k");
                    //result = result.Replace ("|{", "k");
                    //result = result.Replace ("|(", "k");

                    // L
                    //result = result.Replace ("|_", "l");
                    //result = result.Replace ("][_", "l");

                    // M
                    //result = result.Replace ("/\\/\\", "m");
                    //result = result.Replace ("/v\\", "m");
                    //result = result.Replace ("|V|", "m");
                    //result = result.Replace ("]V[", "m");
                    //result = result.Replace ("|\\/|", "m");

                    // N
                    //result = result.Replace ("|\\|", "n");
                    //result = result.Replace ("/\\/", "n");
                    //result = result.Replace ("/V", "n");

                    // O
                    result = result.Replace("o", "0");
                    //result = result.Replace ("()", "o"); 

                    // P
                    //result = result.Replace ("|°", "p");
                    //result = result.Replace ("|>", "p");

                    // Q

                    // R
                    result = result.Replace("r", "2");
                    //result = result.Replace("®", "r");

                    // S
                    result = result.Replace("s", "$");
                    //result = result.Replace("s", "5");
                    //result = result.Replace("§", "s");

                    // T
                    result = result.Replace("t", "7");
                    //result = result.Replace("+", "t");
                    //result = result.Replace("†", "t");
                    //result = result.Replace ("']['", "t");

                    // U
                    //result = result.Replace ("µ", "u");
                    //result = result.Replace ("|_|", "u");

                    // V
                    //result = result.Replace ("\\/", "v");

                    // W
                    //result = result.Replace ("\\/\\/", "w");

                    // X
                    //result = result.Replace ("><", "x");
                    //result = result.Replace (")(", "x");

                    // Y
                    //result = result.Replace("¥", "y");

                    // Z
                }
            }

            //Debug.LogWarning (result);

            return result;
        }
*/

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
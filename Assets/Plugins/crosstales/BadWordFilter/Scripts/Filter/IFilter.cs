namespace Crosstales.BWF.Filter
{
   /// <summary>Interface for all filters.</summary>
   public interface IFilter
   {
      #region Properties

      /// <summary>All sources of the current filter.</summary>
      /// <returns>List with all sources for the current filter</returns>
      System.Collections.Generic.List<Crosstales.BWF.Data.Source> Sources { get; }

      /// <summary>Checks the readiness status of the current filter.</summary>
      /// <returns>True if the filter is ready.</returns>
      bool isReady { get; }

      #endregion


      #region Methods

      /// <summary>Searches for bad words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>True if a match was found</returns>
      bool Contains(string text, params string[] sourceNames);

      /// <summary>Searches for bad words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>List with all the matches</returns>
      System.Collections.Generic.List<string> GetAll(string text, params string[] sourceNames);

      /// <summary>Searches and replaces all bad words in a text.</summary>
      /// <param name="text">Text to check</param>
      /// <param name="markOnly">Only mark the words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found bad word (optional)</param>
      /// <param name="postfix">Postfix for every found bad word (optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>Clean text</returns>
      string ReplaceAll(string text, bool markOnly = false, string prefix = "", string postfix = "", params string[] sourceNames);

      /// <summary>Marks the text with a prefix and postfix.</summary>
      /// <param name="text">Text containing bad words</param>
      /// <param name="replace">Replace the bad words (default: false, optional)</param>
      /// <param name="prefix">Prefix for every found bad word (default: bold and red, optional)</param>
      /// <param name="postfix">Postfix for every found bad word (default: bold and red, optional)</param>
      /// <param name="sourceNames">Relevant sources (e.g. "english", optional)</param>
      /// <returns>Text with marked domains</returns>
      string Mark(string text, bool replace = false, string prefix = "<b><color=red>", string postfix = "</color></b>", params string[] sourceNames);

      /// <summary>Unmarks the text with a prefix and postfix.</summary>
      /// <param name="text">Text with marked bad words</param>
      /// <param name="prefix">Prefix for every found bad word (optional)</param>
      /// <param name="postfix">Postfix for every found bad word (optional)</param>
      /// <returns>Text with marked bad words</returns>
      string Unmark(string text, string prefix = "<b><color=red>", string postfix = "</color></b>");

      #endregion
   }
}
// © 2018-2022 crosstales LLC (https://www.crosstales.com)
using UnityEngine;
using Crosstales.BWF.Util;

namespace Crosstales.BWF.Data
{
   /// <summary>Data definition of a source.</summary>
   [System.Serializable]
   [HelpURL("https://www.crosstales.com/media/data/assets/badwordfilter/api/class_crosstales_1_1_b_w_f_1_1_data_1_1_source.html")]
   [CreateAssetMenu(fileName = "New Source", menuName = Constants.ASSET_NAME + "/Source", order = 1000)]
   public class Source : ScriptableObject
   {
      #region Variables

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("Name"), SerializeField, Header("Information"), Tooltip("Name of the source.")]
      private string sourceName = string.Empty;

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("Culture"), SerializeField, Tooltip("Culture of the source (ISO 639-1).")]
      private string culture = string.Empty;

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("Description"), SerializeField, Tooltip("Description for the source (optional).")]
      private string description = string.Empty;

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("Icon"), SerializeField, Tooltip("Icon to represent the source (e.g. country flag, optional)")]
      private Sprite icon;


      [UnityEngine.Serialization.FormerlySerializedAsAttribute("URL"), SerializeField, Header("Settings"), Tooltip("URL of a text file containing all regular expressions for this source. Add also the protocol-type ('http://', 'file://' etc.).")]
      private string url = string.Empty;

      [UnityEngine.Serialization.FormerlySerializedAsAttribute("Resource"), SerializeField, Tooltip("Text file containing all regular expressions for this source.")]
      private TextAsset resource;

      #endregion


      #region Properties

      /// <summary>Name of the source.</summary>
      public string SourceName
      {
         get => sourceName;
         set => sourceName = value;
      }

      /// <summary>Culture of the source (ISO 639-1).</summary>
      public string Culture
      {
         get => culture;
         set => culture = value;
      }

      /// <summary>Description for the source (optional).</summary>
      public string Description
      {
         get => description;
         set => description = value;
      }

      /// <summary>Icon to represent the source (e.g. country flag, optional)</summary>
      public Sprite Icon
      {
         get => icon;
         set => icon = value;
      }

      /// <summary>URL of a text file containing all regular expressions for this source. Add also the protocol-type ('http://', 'file://' etc.).</summary>
      public string URL
      {
         get => url;
         set => url = value;
      }

      /// <summary>Text file containing all regular expressions for this source.</summary>
      public TextAsset Resource
      {
         get => resource;
         set => resource = value;
      }

      /// <summary>Total Regex count.</summary>
      public int RegexCount { get; set; }

      #endregion


      #region Overridden methods

      public override string ToString()
      {
         System.Text.StringBuilder result = new System.Text.StringBuilder();

         result.Append(GetType().Name);
         result.Append(Constants.TEXT_TOSTRING_START);

         result.Append("Name='");
         result.Append(sourceName);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("Culture='");
         result.Append(culture);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("Description='");
         result.Append(description);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("Icon='");
         result.Append(icon);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("URL='");
         result.Append(url);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("Resource='");
         result.Append(resource);
         result.Append(Constants.TEXT_TOSTRING_DELIMITER_END);

         result.Append(Constants.TEXT_TOSTRING_END);

         return result.ToString();
      }

      public override bool Equals(object obj)
      {
         if (obj == null || GetType() != obj.GetType())
            return false;

         Source o = (Source)obj;

         return sourceName == o.sourceName &&
                culture == o.culture &&
                description == o.description &&
                url == o.url &&
                resource == o.resource;
      }

      public override int GetHashCode()
      {
         int hash = 0;

         if (sourceName != null)
            hash += sourceName.GetHashCode();
         if (culture != null)
            hash += culture.GetHashCode();
         if (description != null)
            hash += description.GetHashCode();
         if (url != null)
            hash += url.GetHashCode();
         if (resource != null)
            hash += resource.GetHashCode();

         return hash;
      }

      #endregion
   }
}
// © 2018-2022 crosstales LLC (https://www.crosstales.com)
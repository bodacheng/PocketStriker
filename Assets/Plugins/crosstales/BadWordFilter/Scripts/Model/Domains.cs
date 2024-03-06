namespace Crosstales.BWF.Model
{
   /// <summary>Model for a source of domains.</summary>
   [System.Serializable]
   public class Domains
   {
      #region Variables

      /// <summary>Source-object.</summary>
      public Crosstales.BWF.Data.Source Source;

      /// <summary>List of all domains (RegEx).</summary>
      public System.Collections.Generic.List<string> DomainList = new System.Collections.Generic.List<string>();

      #endregion


      #region Constructor

      /// <summary>Instantiate the class.</summary>
      /// <param name="source">Source-object.</param>
      /// <param name="domainList">List of all domains (RegEx).</param>
      public Domains(Crosstales.BWF.Data.Source source, System.Collections.Generic.IEnumerable<string> domainList)
      {
         Source = source;

         foreach (string domain in domainList)
         {
            DomainList.Add(domain.Split('#')[0]);
         }
      }

      #endregion


      #region Overridden methods

      public override string ToString()
      {
         System.Text.StringBuilder result = new System.Text.StringBuilder();

         result.Append(GetType().Name);
         result.Append(Crosstales.BWF.Util.Constants.TEXT_TOSTRING_START);

         result.Append("Source='");
         result.Append(Source);
         result.Append(Crosstales.BWF.Util.Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("DomainList='");
         result.Append(DomainList.Count);
         result.Append(Crosstales.BWF.Util.Constants.TEXT_TOSTRING_DELIMITER_END);

         result.Append(Crosstales.BWF.Util.Constants.TEXT_TOSTRING_END);

         return result.ToString();
      }

      public override bool Equals(object obj)
      {
         if (obj == null || GetType() != obj.GetType())
            return false;

         Crosstales.BWF.Model.BadWords o = (Crosstales.BWF.Model.BadWords)obj;

         return Source == null || Source.Equals(o.Source);
      }

      public override int GetHashCode()
      {
         return base.GetHashCode();
      }

/*
      public override int GetHashCode()
      {
         int hash = 0;

         if (Source != null)
            hash += Source.GetHashCode();
         if (DomainList != null)
            hash += DomainList.GetHashCode();

         return hash;
      }
*/

      #endregion
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
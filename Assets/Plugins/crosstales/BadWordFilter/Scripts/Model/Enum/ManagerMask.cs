using System;

namespace Crosstales.BWF.Model.Enum
{
   /// <summary>Enum for all available managers.</summary>
   [Flags]
   public enum ManagerMask
   {
      None = 0,
      All = 1,
      BadWord = 2,
      Domain = 4,
      Capitalization = 8,
      Punctuation = 16

      //16, 32, 64, 128 (256, 512?) etc.
   }
}
// © 2015-2022 crosstales LLC (https://www.crosstales.com)
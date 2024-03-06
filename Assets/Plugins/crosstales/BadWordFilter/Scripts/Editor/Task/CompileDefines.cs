#if UNITY_EDITOR
using UnityEditor;

namespace Crosstales.BWF.EditorTask
{
   /// <summary>Adds the given define symbols to PlayerSettings define symbols.</summary>
   [InitializeOnLoad]
   public class CompileDefines : Crosstales.Common.EditorTask.BaseCompileDefines
   {
      private const string symbol = "CT_BWF";

      static CompileDefines()
      {
         if (Crosstales.BWF.EditorUtil.EditorConfig.COMPILE_DEFINES)
         {
            addSymbolsToAllTargets(symbol);
         }
         else
         {
            removeSymbolsFromAllTargets(symbol);
         }
      }
   }
}
#endif
// © 2017-2022 crosstales LLC (https://www.crosstales.com)
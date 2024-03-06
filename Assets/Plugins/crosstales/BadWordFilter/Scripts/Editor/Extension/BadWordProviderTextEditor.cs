#if UNITY_EDITOR
using UnityEditor;

namespace Crosstales.BWF.EditorExtension
{
   /// <summary>Custom editor for the 'BadWordProviderText'-class.</summary>
   [CustomEditor(typeof(Crosstales.BWF.Provider.BadWordProviderText))]
   public class BadWordProviderTextEditor : BaseProviderEditor
   {
      //empty
   }
}
#endif
// © 2016-2022 crosstales LLC (https://www.crosstales.com)
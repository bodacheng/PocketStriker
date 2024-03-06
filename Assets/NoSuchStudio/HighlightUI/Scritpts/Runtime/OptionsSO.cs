using UnityEngine;

namespace NoSuchStudio.UI.Highlight {
    /// <summary>
    /// Wrapper class for saving options in Unity.
    /// </summary>
    [CreateAssetMenu(menuName = "No Such Studio/UI Highlight/Options SO")]
    public class OptionsSO : ScriptableObject {
        public Options options;
    }
}
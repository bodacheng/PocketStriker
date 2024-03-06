using NoSuchStudio.Common;
using UnityEngine;
using UnityEngine.UI;

namespace NoSuchStudio.UI.Highlight.Extentions {
    public partial class HighlightUIText : NoSuchMonoBehaviour {

        public const string GameObjectName = "text";

        protected static HighlightUIText instance; // set by the instance itself
        public static HighlightUIText Instance {
            get {
                return instance;
            }
        }

        public static HighlightUIText GetInstance() {
            return instance;
        }

        public static void Show(string txt) {
            if (Instance == null) return;
            Instance.DoShow(txt);
        }
    }
}

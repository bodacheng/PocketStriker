using UnityEngine;
using UnityEngine.UI;
using NoSuchStudio.Common;
namespace NoSuchStudio.UI.Highlight.Extentions {
    public partial class HighlightUIText : NoSuchMonoBehaviour {
        [SerializeField] GameObject textPrefab;

        private void Awake() {
            instance = this;
        }

        private void OnDestroy() {
            if (instance == this) {
                instance = null;
            }
        }
        public RectTransform DoGetText() {
            var textName = "text";
            var highlightT = HighlightUI.GetInstance().transform;
            var t = highlightT.Find(textName);
            if (t) {
                // NOOP
            } else {
                var textObj = Instantiate(textPrefab, highlightT);
                textObj.name = textName;
                t = textObj.transform;
            }
            // Cannot really create text from code, need a font. Hence the prefab.
            /*
            Text text = t.GetComponent<Text>();
            if (!text) {
                text = t.gameObject.AddComponent<Text>();
                text.supportRichText = true;
                text.resizeTextForBestFit = true;
                text.maskable = false;
                text.raycastTarget = false;
            }*/

            return t as RectTransform;
        }

        /// <summary>
        /// Position the text on the opener side of the highlight frame.
        /// </summary>
        /// <param name="txt">text value to show.</param>
        public void DoShow(string txt) {
            var rtText = DoGetText();
            var text = rtText.GetComponent<Text>();
            text.text = txt;
            Padding p = HighlightUI.GetInstance().padding;
            float centerWidth = Screen.width - p.left - p.right;
            float centerHeight = Screen.height - p.top - p.bottom;
            float sideArea = 2 * (Mathf.Min(p.top, p.bottom) + centerHeight / 2) * Mathf.Max(p.left, p.right);
            float overArea = 2 * (Mathf.Min(p.left, p.right) + centerWidth / 2) * Mathf.Max(p.top, p.bottom);
            if (sideArea > overArea) {
                bool leftSide = p.left >= p.right;
                rtText.anchorMin = new Vector2(0.5f, 0.5f);
                rtText.anchorMax = new Vector2(0.5f, 0.5f);
                rtText.pivot = new Vector2(0.5f, 0.5f);
                float posX = leftSide ? -(Screen.width - p.left) / 2 : (Screen.width - p.right) / 2;
                rtText.anchoredPosition = new Vector2(posX, (p.bottom - p.top) / 2);
                rtText.sizeDelta = new Vector2(Mathf.Max(p.left, p.right), centerHeight + Mathf.Min(p.top, p.bottom));
                text.alignment = leftSide ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            } else {
                bool topSide = p.top >= p.bottom;
                rtText.anchorMin = new Vector2(0.5f, 0.5f);
                rtText.anchorMax = new Vector2(0.5f, 0.5f);
                rtText.pivot = new Vector2(0.5f, 0.5f);
                float posY = topSide ? (Screen.height - p.top) / 2 : -(Screen.height - p.bottom) / 2;
                rtText.anchoredPosition = new Vector2((p.left - p.right) / 2, posY);
                rtText.sizeDelta = new Vector2(centerWidth + Mathf.Min(p.left, p.right), Mathf.Max(p.top, p.bottom));
                text.alignment = topSide ? TextAnchor.LowerCenter : TextAnchor.UpperCenter;
            }
        }
    }
}

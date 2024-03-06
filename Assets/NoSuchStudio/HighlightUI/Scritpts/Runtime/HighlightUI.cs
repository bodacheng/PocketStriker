using NoSuchStudio.Common;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

namespace NoSuchStudio.UI.Highlight {
    public partial class HighlightUI : NoSuchMonoBehaviour {

        public Options options; // left, top, right, bottom

        private Coroutine fadeAnimation;
        private Action fadeAnimationEndAction;

        private Padding _padding;
        /// <summary>
        /// Get coordinates of the highlight frame in <see cref="Screen"/> coordinates. 
        /// Defined by padding from each screen edge.
        /// Is valid only if highlight UI <see cref="IsShowing"/>.
        /// </summary>
        public Padding padding {
            get { 
                return _padding; 
            }
        }

        bool isShowing {
            get { return canvas && canvas.enabled; }
        }

        /*public Padding padding {
            get { return options.padding; }
        }*/

        Canvas canvas;
        private void FastForwardPendingAnimation() {
            if (fadeAnimation != null) {
                StopCoroutine(fadeAnimation);
                Action tmpAction = fadeAnimationEndAction;
                fadeAnimation = null;
                fadeAnimationEndAction = null;
                tmpAction?.Invoke();
            }
        }

        private void SetOptions(Options options) {
            this.options = new Options(options);
            GetInstance();
            canvas = GetComponent<Canvas>();
            canvas.sortingOrder = options.canvasSortOrder;
            SetColor(this.options.color);
        }

        private RectTransform GetPanel(string n) {
            var fullName = $"panel{n}";
            var t = transform.Find(fullName);
            if (t) {
                // NOOP
            } else {
                var panelObj = new GameObject(fullName, typeof(RectTransform));
                t = panelObj.transform;
                t.SetParent(transform);
                t = panelObj.transform;
                // LogLog($"t is null? {t == null} t as rt {t as RectTransform}");
            }
            SetupPanel(t as RectTransform, n);
            return t as RectTransform;
        }

        private void OnPointerDown() {
            if (options.dismissOnClick) {
                DoDismiss(true);
            }
        }

        private void SetupPanel(RectTransform rt, string pn) {
            
            if (!rt.GetComponent<Image>()) {
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = true;
            }

            var pdh = rt.GetComponent<PointerDownHandler>();
            if (!pdh) {
                pdh = rt.gameObject.AddComponent<PointerDownHandler>();
            }

            pdh.pointerEvent.RemoveAllListeners();
            pdh.pointerEvent.AddListener(OnPointerDown);

            switch (pn) {
                case "Top":
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    break;
                case "Bottom":
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    break;
                case "Left":
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    break;
                case "Right":
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    break;
                case "Center":
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 0f);

                    Image frameImg = rt.GetComponent<Image>();
                    frameImg.sprite = options.frameSprite;
                    frameImg.type = options.frameType;
                    frameImg.alphaHitTestMinimumThreshold = 0.1f;
                    break;
            }
        }

        private void SetColor(Color c) {
            GetPanel("Top").GetComponent<Image>().color = c;
            GetPanel("Bottom").GetComponent<Image>().color = c;
            GetPanel("Left").GetComponent<Image>().color = c;
            GetPanel("Right").GetComponent<Image>().color = c;
            if (options.frameSprite != null) {
                GetPanel("Center").GetComponent<Image>().color = c;
            }
        }

        /// <summary>
        /// Show highlight UI for a 3D object. Uses the <see cref="Renderer"/> components bounds.
        /// </summary>
        /// <remarks>
        /// For UI objects (RectTransforms) use <see cref="DoShowForUI(RectTransform, Options)"/>.
        /// </remarks>
        /// <param name="t"></param>
        /// <param name="options"></param>
        private void DoShowFor3DObject(Transform t, BoundsMode boundsMode, Options options) {
            Bounds b = GetTransformBounds(t, boundsMode, GetComponent<Canvas>().worldCamera);
            Rect r = new Rect();
            r.xMin = b.min.x;
            r.xMax = b.max.x;
            r.yMin = b.min.y;
            r.yMax = b.max.y;
            DoShowForScreenRect(r, options);
        }

        /// <summary>
        /// Show highlight UI for a RectTransform. Any Unity UI is effectively supported.
        /// </summary>
        /// <param name="rt"></param>
        /// <param name="options"></param>
        private void DoShowForUI(RectTransform rt, Options options) {
            Bounds b = GetRectTransformBounds(rt);
            Rect r = new Rect();
            r.xMin = b.min.x;
            r.xMax = b.max.x;
            r.yMin = b.min.y;
            r.yMax = b.max.y;
            DoShowForScreenRect(r, options);
        }

        /// <summary>
        /// Specify where on screen to highlight. Use for non UI elements, i.e. if you want to highlight a 3D object.
        /// </summary>
        /// <param name="rect">coordinates for the highlighted rect in screen space.</param>
        private void DoShowForScreenRect(Rect rect, Options options) {
            FastForwardPendingAnimation();
            if (IsShowing) DoDismissImmediate(true);
            SetOptions(options);

            float top = Screen.height - rect.yMax - options.padding.top;
            float bottom = rect.yMin - options.padding.bottom;
            float left = rect.xMin - options.padding.left;
            float right = Screen.width - rect.xMax - options.padding.right;
            _padding = new Padding(top, bottom, left, right);
            var rtTop = GetPanel("Top");
            rtTop.sizeDelta = new Vector2(0f, top);
            
            var rtBottom = GetPanel("Bottom");
            rtBottom.sizeDelta = new Vector2(0f, bottom);

            float height = top + bottom;
            float centerWidth = Screen.width - (left + right);
            float centerHeight = Screen.height - (top + bottom);
            var rtLeft = GetPanel("Left");
            rtLeft.sizeDelta = new Vector2(left, -height);
            rtLeft.anchoredPosition = new Vector2(0f, (bottom - top) / 2);

            var rtRight = GetPanel("Right");
            rtRight.sizeDelta = new Vector2(right, -height);
            rtRight.anchoredPosition = new Vector2(0f, (bottom - top) / 2);

            if (options.frameSprite != null) {
                var rtCenter = GetPanel("Center");
                rtCenter.sizeDelta = new Vector2(centerWidth, centerHeight);
                rtCenter.anchoredPosition = new Vector2((left - right) / 2, (bottom - top) / 2);
            }

            canvas = GetComponent<Canvas>();
            canvas.enabled = true;

            fadeAnimationEndAction = null;
            fadeAnimation = StartCoroutine(FadeAnimation(true));
        }

        /// <summary>
        /// Coroutine for fading the highlight canvas in and out.
        /// </summary>
        /// <param name="fadeIn">if true fade in, else fade out.</param>
        /// <returns></returns>
        IEnumerator FadeAnimation(bool fadeIn) {
            float startTime = Time.unscaledTime;
            float endTime = startTime + options.fadeDuration;
            var cg = canvas.GetComponent<CanvasGroup>();
            while (Time.unscaledTime < endTime) {
                float t = Mathf.Lerp(0f, 1f, (Time.unscaledTime - startTime) / (endTime - startTime));
                cg.alpha = fadeIn ? t : 1 - t;
                yield return null;
            }
            cg.alpha = fadeIn ? 1f : 0f;
            Action tmpAction = fadeAnimationEndAction;
            fadeAnimation = null;
            fadeAnimationEndAction = null;
            tmpAction?.Invoke();
        }

        /// <summary>
        /// Start the fade out animation. At the end of the the animation <see cref="DoDismissImmediate(bool)"/> will be called to 
        /// actually disable the highlight canvas.
        /// </summary>
        /// <param name="runDismissCallback">Can be used to dismiss the highlight UI without the callback getting called.</param>
        public void DoDismiss(bool runDismissCallback = true) {
            if (!isShowing) return;
            FastForwardPendingAnimation();
            fadeAnimationEndAction = () => {
                DoDismissImmediate(runDismissCallback);
                fadeAnimation = null;
                fadeAnimationEndAction = null;
            };
            fadeAnimation = StartCoroutine(FadeAnimation(false));
        }

        /// <summary>
        /// Disables the highlight canvas, hiding the highlight UI.
        /// </summary>
        /// <param name="runDismissCallback"></param>
        private void DoDismissImmediate(bool runDismissCallback) {
            canvas = GetComponent<Canvas>();
            if (canvas && canvas.enabled) {
                canvas.enabled = false;
                if (runDismissCallback && options != null && options.dismissAction != null) {    
                    options.dismissAction();
                }
            } else {
                LogWarn("dismiss called without showing the highlight.");
            }
            _padding = default;
        }
    }
}

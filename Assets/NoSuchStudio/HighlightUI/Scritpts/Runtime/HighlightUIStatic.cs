using NoSuchStudio.Common;
using UnityEngine;
using UnityEngine.UI;

namespace NoSuchStudio.UI.Highlight {
    public enum BoundsMode {
        Collider,
        Renderer
    }
    public partial class HighlightUI : NoSuchMonoBehaviour {

        public const string GameObjectName = "NoSuchStudio_HighlightCanvas";

        public static Options defaultOptions = new Options();

        public static HighlightUI Instance {
            get {
                return GetInstance();
            }
        }

        public static bool IsShowing {
            get {
                return Instance.isShowing;
            }
        }

        public static Padding Padding {
            get {
                return Instance.padding;
            }
        }

        public static HighlightUI GetInstance() {
            var canvasObj = GameObject.Find(GameObjectName);
            if (!canvasObj) {
                canvasObj = new GameObject(GameObjectName);
            }
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (!canvas) {
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var gr = canvasObj.AddComponent<GraphicRaycaster>();
                gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;

                var cg = canvasObj.AddComponent<CanvasGroup>();
                cg.interactable = true;
                cg.alpha = 1f;
            }
            HighlightUI highlight = canvasObj.GetComponent<HighlightUI>();
            if (!highlight) {
                highlight = canvasObj.AddComponent<HighlightUI>();
            }
            return highlight;
        }

        public static Bounds GetTransformBounds(Transform t, BoundsMode boundsMode, Camera cam) {
            Bounds worldBounds = new Bounds(t.position, Vector3.one);
            switch (boundsMode) {
                case BoundsMode.Collider:
                    var col = t.GetComponent<Collider>();
                    if (col) worldBounds = col.bounds;
                    break;
                case BoundsMode.Renderer:
                    var ren = t.GetComponent<Renderer>();
                    if (ren) worldBounds = ren.bounds;
                    break;
            }

            Vector3[] worldCorners = new Vector3[8];
            worldCorners[0] = new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z);
            worldCorners[1] = new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z);
            worldCorners[2] = new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z);
            worldCorners[3] = new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z);
            worldCorners[4] = new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z);
            worldCorners[5] = new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z);
            worldCorners[6] = new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z);
            worldCorners[7] = new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z);
            Bounds bounds = new Bounds(RectTransformUtility.WorldToScreenPoint(cam ?? Camera.main, worldCorners[0]), Vector3.zero);
            for (int i = 1; i < 8; ++i) {
                Vector3 corner = RectTransformUtility.WorldToScreenPoint(cam ?? Camera.main, worldCorners[i]);
                bounds.Encapsulate(corner);
            }
            return bounds;
        }
        public static Bounds GetRectTransformBounds(RectTransform rt) {
            Vector3[] WorldCorners = new Vector3[4];
            rt.GetWorldCorners(WorldCorners);

            // Bounds bounds = new Bounds(WorldCorners[0], Vector3.zero);
            Bounds bounds = new Bounds(RectTransformUtility.WorldToScreenPoint(rt.GetComponentInParent<Canvas>().worldCamera, WorldCorners[0]), Vector3.zero);
            for (int i = 1; i < 4; ++i) {
                Vector3 corner = RectTransformUtility.WorldToScreenPoint(rt.GetComponentInParent<Canvas>().worldCamera, WorldCorners[i]);
                bounds.Encapsulate(corner);
                //bounds.Encapsulate(WorldCorners[i]);
            }
            return bounds;
        }

        public static void ShowFor3DObject(Transform t, BoundsMode boundsMode = BoundsMode.Collider, Options options = null) {
            options = options ?? defaultOptions;
            var h = GetInstance();
            h.DoShowFor3DObject(t, boundsMode, options);
        }

        public static void ShowForUI(RectTransform rt, Options options = null) {
            options = options ?? defaultOptions;
            var h = GetInstance();
            h.DoShowForUI(rt, options);
        }

        public static void ShowForFixedRect(Rect rect, Options options = null) {
            options = options ?? defaultOptions;
            var h = GetInstance();
            h.DoShowForScreenRect(rect, options);
        }

        public static void Dismiss(bool runDismissCallback = true) {
            var h = GetInstance();
            h.DoDismiss(runDismissCallback);
        }
    }
}

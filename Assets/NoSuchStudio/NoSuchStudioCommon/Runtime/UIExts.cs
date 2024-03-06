using UnityEngine;
using UnityEngine.UI;

namespace NoSuchStudio.Common {
    public static class UIExts {
        // got from https://stackoverflow.com/questions/30766020/how-to-scroll-to-a-specific-element-in-scrollrect-with-unity-ui
        public static Vector2 GetSnapToPositionToBringChildIntoView(this ScrollRect instance, RectTransform child) {
            Canvas.ForceUpdateCanvases();
            Vector2 viewportLocalPosition = instance.viewport.localPosition;
            Vector2 childLocalPosition = child.localPosition;
            Vector2 result = new Vector2(
                0 - (viewportLocalPosition.x + childLocalPosition.x),
                0 - (viewportLocalPosition.y + childLocalPosition.y)
            );
            return result;
        }
    }
}

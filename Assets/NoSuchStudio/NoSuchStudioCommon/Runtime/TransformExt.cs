using UnityEngine;
using System.Collections;

namespace NoSuchStudio.Common {
    public static class TransformExt {

        public static void ClearChildren(this Transform t) {
            if (Application.isEditor && !Application.isPlaying) {
                for (int i = t.childCount - 1; i >= 0; i--) {
                    GameObject.DestroyImmediate(t.GetChild(i).gameObject);
                }
            } else {
                for (int i = t.childCount - 1; i >= 0; i--) {
                    GameObject.Destroy(t.GetChild(i).gameObject);
                }
            }
        }
    }
}
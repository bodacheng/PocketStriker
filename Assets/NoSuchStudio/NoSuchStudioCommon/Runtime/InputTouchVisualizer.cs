using System;

using UnityEngine;

namespace NoSuchStudio.Common {
    public class InputTouchVisualizer : MonoBehaviour {

        public GameObject prefab;

        GameObject[] touchVisuals;
        int maxTouchCount = 10;

        void Start() {
            touchVisuals = new GameObject[maxTouchCount];
            for (int i = 0; i < maxTouchCount; i++) {
                touchVisuals[i] = Instantiate(prefab, transform, false);
                touchVisuals[i].name = string.Format("TouchVisualizer{0}", i);
                touchVisuals[i].transform.SetAsLastSibling();
            }
        }

        void Update() {
            int c = Math.Min(Input.touchCount, maxTouchCount);
            for (int i = 0; i < c; i++) {
                Touch t = Input.GetTouch(i);
                Vector3 worldPos = Camera.main.ScreenToWorldPoint((Vector3)t.position + Vector3.forward * -1);
                touchVisuals[i].transform.position = worldPos;
                touchVisuals[i].SetActive(true);
            }

            for (int i = c; i < maxTouchCount; i++) {
                touchVisuals[i].SetActive(false);
            }
        }
    }

}
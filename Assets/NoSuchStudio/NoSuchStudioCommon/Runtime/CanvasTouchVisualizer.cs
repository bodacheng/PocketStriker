using UnityEngine;
using System;
namespace NoSuchStudio.Common {
    public class CanvasTouchVisualizer : MonoBehaviour {
        Canvas mainCanvas;
        Camera mainCamera;
        public GameObject prefab;
        GameObject[] touchVisuals;
        int maxTouchCount = 5;
        // Start is called before the first frame update
        void Start() {
            mainCanvas = GameObject.FindWithTag("MainCanvas").GetComponent<Canvas>();
            mainCamera = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();

            touchVisuals = new GameObject[maxTouchCount];

            for (int i = 0; i < maxTouchCount; i++) {
                touchVisuals[i] = Instantiate(prefab, mainCanvas.transform, false);
                touchVisuals[i].name = string.Format("TouchVisualizer{0}", i);
                touchVisuals[i].transform.SetAsLastSibling();
            }
        }

        // Update is called once per frame
        void Update() {
            for (int i = 0; i < Math.Min(Input.touchCount, maxTouchCount); i++) {
                Vector3 globalPos = new Vector3();
                RectTransformUtility.ScreenPointToWorldPointInRectangle(mainCanvas.transform as RectTransform, Input.touches[i].position, mainCamera, out globalPos);
                touchVisuals[i].transform.position = globalPos;
                touchVisuals[i].SetActive(true);
            }

            for (int i = Input.touchCount; i < maxTouchCount; i++) {
                touchVisuals[i].SetActive(false);
            }
        }
    }
}
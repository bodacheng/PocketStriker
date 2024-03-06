using NoSuchStudio.Common;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NoSuchStudio.UI.Highlight {
    public class PointerDownHandler : NoSuchMonoBehaviour, IPointerDownHandler {
        public UnityEvent pointerEvent = new UnityEvent();
        public void OnPointerDown(PointerEventData eventData) {
            pointerEvent.Invoke();
        }
    }
}

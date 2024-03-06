/*
// Don't want the common library to depend on TextMeshPro. Need script defines or another mechanism to automatically include / exclude depending on the target project.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace NoSuchStudio.Common {
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TMPLinkHandler : NoSuchMonoBehaviour, IPointerClickHandler {
        TextMeshProUGUI tmpro;
        void Awake() {
            tmpro = GetComponent<TextMeshProUGUI>();
        }

        public void OnPointerClick(PointerEventData eventData) {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpro, Input.mousePosition, Camera.main);
            if (linkIndex != -1) { // was a link clicked?
                TMP_LinkInfo linkInfo = tmpro.textInfo.linkInfo[linkIndex];

                // open the link id as a url, which is the metadata we added in the text field
                Application.OpenURL(linkInfo.GetLinkID());
            } else {
                LogLog("click tmpro no links");
            }
        }
    }
}*/
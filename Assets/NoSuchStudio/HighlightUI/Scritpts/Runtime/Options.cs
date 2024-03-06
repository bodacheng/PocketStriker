using NoSuchStudio.Common;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using System;

namespace NoSuchStudio.UI.Highlight {
    
    [Serializable]
    public class Options {
        public static readonly UnityAction ActionNOP = () => { };
        /// <summary>
        /// Highlight UI can always be dismissed programmatically by calling <see cref="HighlightUI.Dismiss(bool)"/>.
        /// If dissmissOnClick is set then clicking outside the highlight frame will dismiss the highlight UI and invoke the dismiss callback.
        /// </summary>
        [Header("Behaviour")]
        public bool dismissOnClick;
        public UnityAction dismissAction;

        /// <summary>
        /// The highlight UI is shown in its own screen overlay canvas. This sets the canvas sort order.
        /// </summary>
        public int canvasSortOrder;

        /// <summary>
        /// When shown with <see cref="HighlightUI.ShowForUI(RectTransform)"/>, the hint box will
        /// dynamically track the target recttransform. Suitable for screen rotations on mobile or 
        /// other cases where the UI might layout.
        /// </summary>
        // public bool handleResize;

        /// <summary>
        /// The color for the highlight UI. You can use the alpha channel.
        /// </summary>
        [Header("Visual")]
        public Color color;
        /// <summary>
        /// Padding for the frame around the highlighted RectTransform;
        /// </summary>
        public Padding padding;
        /// <summary>
        /// Fade in and fade out duration of the highlight UI. Use 0f for no fade animation.
        /// </summary>
        public float fadeDuration;
        /// <summary>
        /// Sprite to use for the frame around the highlighted UI. null will show an empty rect.
        /// </summary>
        public Sprite frameSprite;
        /// <summary>
        /// You can use both sliced sprites or simple sprites for the frame.
        /// </summary>
        public Image.Type frameType;

        public Options() {
            dismissOnClick = true;
            dismissAction = ActionNOP;
            color = Color.white;
            padding = new Padding(50, 50, 50, 50);
            fadeDuration = 0.25f;
            frameSprite = null;
            frameType = Image.Type.Simple;
        }

        public Options(Options o) {
            dismissOnClick = o.dismissOnClick;
            dismissAction = o.dismissAction;
            canvasSortOrder = o.canvasSortOrder;
            color = o.color;
            padding = o.padding;
            fadeDuration = o.fadeDuration;
            frameSprite = o.frameSprite;
            frameType = o.frameType;
        }
    }
}
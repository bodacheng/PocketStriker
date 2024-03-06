using System;

namespace NoSuchStudio.UI.Highlight {
    [Serializable]
    public struct Padding {
        public float top;
        public float right;
        public float left;
        public float bottom;

        public void SetHorizontal(float v) {
            right = v;
            left = v;
        }

        public void SetVertical(float v) {
            top = v;
            bottom = v;
        }

        public void SetAll(float v) {
            top = bottom = left = right = v;
        }

        public Padding(float top, float bottom, float left, float right) {
            this.top = top;
            this.bottom = bottom;
            this.left = left;
            this.right = right;
        }
    }
}

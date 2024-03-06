using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace NoSuchStudio.Common {
    public static class Helpers {
        public static bool IsEditMode {
            get { return (Application.isEditor && !Application.isPlaying); }
        }

        public static bool IsTablet() {
            // Compute screen size
            float screenWidth = Screen.width / Screen.dpi;
            float screenHeight = Screen.height / Screen.dpi;
            double size = Mathf.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);
            // Tablet devices should have a screen size greater than 6 inches
            return size >= 6;
        }

        public static T Random<T>(this List<T> list) {
            if (list.Count == 0) return default;

            int i = UnityEngine.Random.Range(0, list.Count);
            return list[i];
        }

        /**
         * return c unique random integers in range [0, max)
         * */
        public static List<int> UniqueRandom(int c, int min, int max) {
            if (c >= (max - min - 1) / 2) {
                throw new IllegalStateException(string.Format("UniqueRandom inefficient for c: {0}, max: {1}", c, max));
            }
            List<int> ret = new List<int>(c);
            HashSet<int> curSet = new HashSet<int>();
            while (ret.Count < c) {
                int rand = UnityEngine.Random.Range(min, max);
                if (!curSet.Contains(rand)) {
                    curSet.Add(rand);
                    ret.Add(rand);
                }
            }
            return ret;
        }
    }
}

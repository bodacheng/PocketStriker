using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NoSuchStudio.Common {
    public class EditorUtilities
    {
        /*public static bool IsInPrefabMode {
            get {
#if UNITY_2018_3_OR_NEWER && UNITY_EDITOR
                var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                return stage != null;
#else
                return false;
#endif
            }
        }*/

        public static bool IsInMainStage(GameObject go) {
#if UNITY_EDITOR
            var mainStage = UnityEditor.SceneManagement.StageUtility.GetMainStageHandle();
            var currentStage = UnityEditor.SceneManagement.StageUtility.GetStageHandle(go);
            return currentStage == mainStage;
#else
            return true;
#endif
        }
    }
}

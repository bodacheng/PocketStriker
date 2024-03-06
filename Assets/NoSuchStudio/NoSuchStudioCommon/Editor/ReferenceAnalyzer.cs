using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using UnityEditor;

namespace NoSuchStudio.Common.Editor {
    public class ReferenceAnalyzer {
        // https://www.gamasutra.com/blogs/LiorTal/20141208/231690/Finding_Missing_Object_References_in_Unity.php
        [MenuItem("Tools/Find Missing references in scene")]
        public static void FindMissingReferences() {
            var objects = GameObject.FindObjectsOfType<GameObject>();

            foreach (var go in objects) {
                var components = go.gameObject.GetComponents<Component>();

                foreach (var c in components) {
                    SerializedObject so = new SerializedObject(c);
                    var sp = so.GetIterator();

                    while (sp.NextVisible(true)) {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference) {
                            if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0) {
                                ShowError(go, sp.name);
                            }
                        }
                    }
                }
            }
        }

        private static void ShowError(GameObject go, string propertyName) {
            Debug.LogFormat(LogType.Error, LogOption.None, go, "Missing reference found in {0} -> {1}", FullObjectPath(go), propertyName);
        }

        private static string FullObjectPath(GameObject go) {
            return go.transform.parent == null ? go.name : FullObjectPath(go.transform.parent.gameObject) + "/" + go.name;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
namespace NoSuchStudio.Common {
    public class Singleton : NoSuchMonoBehaviour {
        public string tagName;
        public static Dictionary<string, Singleton> _instances = new Dictionary<string, Singleton>();

        public bool IsChosenSingleton {
            get {
                return !string.IsNullOrEmpty(tagName)
                    && _instances.ContainsKey(tagName)
                    && _instances[tagName] == this;
            }
        }

        //Awake is always called before any Start functions
        private void OnEnable() {
            if (string.IsNullOrEmpty(tagName)) throw new ApplicationException(string.Format("Singleton tagName empty for object {0}", gameObject.name));

            if (_instances.ContainsKey(tagName)) {
                gameObject.SetActive(false);
                Destroy(gameObject);
            } else {
                gameObject.tag = tagName;
                _instances[tagName] = this;
                //Sets this to not be destroyed when reloading scene
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDisable() {
            if (_instances.ContainsKey("tagName") && _instances["tagName"] == this) {
                _instances.Remove("tagName");
            }
        }
    }
}
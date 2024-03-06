using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace NoSuchStudio.Common {
    [Serializable]
    public class Scope {
        public static readonly Scope Global = Scope.Create("", "");

        [SerializeField] private string _scope;
        [SerializeField] private string _delimiter;

        public static Scope Create(string scope, string delimiter = ".") {
            return new Scope(scope, delimiter);
        }

        private Scope(string scope, string delimiter) {
            Assert.IsTrue(string.IsNullOrEmpty(scope) || !scope.EndsWith(delimiter));
            _scope = scope;
            _delimiter = delimiter;
        }

        public bool Match(string fullName) {
            return fullName.StartsWith(string.Format("{0}{1}", _scope, _delimiter));
        }

        public string Apply(string partialName) {
            return string.Format("{0}{1}{2}", _scope, _delimiter, partialName);
        }

        public string Unapply(string fullName) {
            if (!Match(fullName)) throw new ApplicationException(string.Format("Cannot unapply {0}{1} to {2}", _scope, _delimiter, fullName));
            return fullName.Substring(_scope.Length + _delimiter.Length);
        }
    }
}
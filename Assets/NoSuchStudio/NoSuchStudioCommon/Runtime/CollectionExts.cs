using UnityEngine;
using System.Collections.Generic;

namespace NoSuchStudio.Common {
    public static class CollectionExts {
        /*public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) {
            TValue retVal;
            dic.TryGetValue(key, out retVal);
            return retVal;
        }*/

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue defVal = default(TValue)) {
            TValue retVal;
            bool found = dic.TryGetValue(key, out retVal);
            if (!found) {
                retVal = defVal;
            }
            return retVal;
        }
    }
}
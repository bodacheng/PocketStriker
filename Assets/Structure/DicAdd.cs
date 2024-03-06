using System.Collections.Generic;
using UnityEngine;

public static class DicAdd<Key,Value>
{
    public static void Add(IDictionary<Key, Value> Dic, Key key, Value value)
    {
        if (key == null)
        {
            Debug.Log("key值严重错误。欲添加的value值："+ value);
            return;
        }
        
        if (Dic.ContainsKey(key))
        {
            Dic[key] = value;
        }else{
            Dic.Add(key,value);
        }
    }
}
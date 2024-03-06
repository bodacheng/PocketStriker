using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 复合字典
/// </summary>
[System.Serializable]
public class MultiDic<Key1, Key2, Value>
{
    /// <summary>
    /// 字典结构
    /// </summary>
    ///
    public Dictionary<(Key1, Key2), Value> mDict = new Dictionary<(Key1, Key2), Value>();
    
    public List<Value> GetValues()
    {
        return mDict.Values.ToList();
    }
    
    /// <summary>
    /// 序列化对象
    /// </summary>
    public SerializableSet[] _SerializableSets;
    
    // 该函数设置为private，成为Set函数内部处理，为了是简化程序
    public SerializableSet[] ConvertDictionaryToSerializableArray()
    {
        var temp = new List<SerializableSet>();
        foreach (var keyValuePair in mDict)
        {
            var serializableSet = new SerializableSet
            {
                key1 = keyValuePair.Key.Item1,
                key2 = keyValuePair.Key.Item2,
                value = keyValuePair.Value
            };
            temp.Add(serializableSet);
        }
        _SerializableSets = temp.ToArray();
        return _SerializableSets;
    }
    
    public Dictionary<(Key1, Key2), Value> ConvertSerializableArrayToDictionary()
    {
        mDict = new Dictionary<(Key1, Key2), Value>();
        foreach(var _set in _SerializableSets)
        {
            mDict.Add((_set.key1, _set.key2), _set.value);
        }
        return mDict;
    }
    
    /// <summary>
    /// 赋值
    /// </summary>
    public void Set(Key1 key1, Key2 key2, Value value)
    {
        if (mDict.ContainsKey((key1, key2)))
        {
            mDict[(key1, key2)] = value;
        }
        else
        {
            mDict.Add((key1, key2), value);
        }
        ConvertDictionaryToSerializableArray();
    }
 
    /// <summary>
    /// 取值
    /// </summary>
    public Value Get(Key1 key1, Key2 key2, Value defaultValue = default)
    {
        if (mDict.ContainsKey((key1, key2)))
        {
            return mDict[(key1, key2)];
        }
        return defaultValue;
    }

    public void Clear()
    {
        mDict.Clear();
        _SerializableSets = null;
    }
    
    [System.Serializable]
    public class SerializableSet
    {
        public Key1 key1;
        public Key2 key2;
        public Value value;
    }
}

public class SSIMultiDictionary
{
    public SSIMultiDictionary()
    {
        Main = new MultiDic<string, string, int>();        
    }
    public readonly MultiDic<string, string, int> Main = new MultiDic<string, string, int>();

    public List<(string, string)> GiveOutMin()
    {
        int min = 999999999;
        List<(string, string)> minkeys = null;
        foreach (var pair in Main.mDict)
        {
            if (pair.Value < min)
            {
                minkeys = new List<(string, string)> { pair.Key };
                min = pair.Value;
            }
            else if (pair.Value == min)
            {
                if (minkeys == null)
                {
                    minkeys = new List<(string, string)>();
                }
                minkeys.Add(pair.Key);
            }
        }
        return minkeys;
    }
}
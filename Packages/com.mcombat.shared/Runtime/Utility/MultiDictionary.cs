using System.Collections.Generic;

[System.Serializable]
public class MultiDic<Key1, Key2, Value>
{
    public Dictionary<(Key1, Key2), Value> mDict = new Dictionary<(Key1, Key2), Value>();
    public SerializableSet[] _SerializableSets;
    public int Count => mDict.Count;
    public ICollection<Value> Values => mDict.Values;

    public List<Value> GetValues()
    {
        return new List<Value>(mDict.Values);
    }

    public SerializableSet[] ConvertDictionaryToSerializableArray()
    {
        var temp = new List<SerializableSet>(mDict.Count);
        foreach (var keyValuePair in mDict)
        {
            temp.Add(new SerializableSet
            {
                key1 = keyValuePair.Key.Item1,
                key2 = keyValuePair.Key.Item2,
                value = keyValuePair.Value
            });
        }

        _SerializableSets = temp.ToArray();
        return _SerializableSets;
    }

    public Dictionary<(Key1, Key2), Value> ConvertSerializableArrayToDictionary()
    {
        mDict = new Dictionary<(Key1, Key2), Value>();
        foreach (var set in _SerializableSets)
        {
            mDict.Add((set.key1, set.key2), set.value);
        }

        return mDict;
    }

    public void Set(Key1 key1, Key2 key2, Value value)
    {
        var key = (key1, key2);
        mDict[key] = value;
        ConvertDictionaryToSerializableArray();
    }

    public Value Get(Key1 key1, Key2 key2, Value defaultValue = default)
    {
        return mDict.TryGetValue((key1, key2), out var value) ? value : defaultValue;
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
    public readonly MultiDic<string, string, int> Main = new MultiDic<string, string, int>();

    public List<(string, string)> GiveOutMin()
    {
        var min = 999999999;
        List<(string, string)> minKeys = null;
        foreach (var pair in Main.mDict)
        {
            if (pair.Value < min)
            {
                minKeys = new List<(string, string)> { pair.Key };
                min = pair.Value;
            }
            else if (pair.Value == min)
            {
                if (minKeys == null)
                {
                    minKeys = new List<(string, string)>();
                }

                minKeys.Add(pair.Key);
            }
        }

        return minKeys;
    }
}

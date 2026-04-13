using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;

public static class EffectsManager
{
    // 以下的重点是主界面和战斗界面通用问题
    static readonly IDictionary<string, DecompositionPool> EffectPoolsDic = new Dictionary<string, DecompositionPool>();
    
    static UniTask<GameObject> TryLoadEffectPrefab(string key)
    {
        if (!AddressablesLogic.CheckKeyExist("effect", key))
        {
            return default;
        }
        else
        {
            var returnValue = AddressablesLogic.LoadT<GameObject>(key);
            return returnValue;
        }
    }
    
    public static void Clear()
    {
        foreach (var pool in EffectPoolsDic)
        {
            pool.Value.Clear();
        }
        EffectPoolsDic.Clear();
    }
    
    public static async UniTask<Decomposition> GenerateEffect(string resourceName, string effectPath, Vector3 pos, Quaternion qua, Transform parentT)
    {
        if (string.IsNullOrEmpty(resourceName))
            return default;
        var effectPool = await IniEffectsPool(resourceName, effectPath, 3);
        if (effectPool == null)
            return default;
        var processingEffectObj = effectPool.Rent();
        var myConstraintSource = new ConstraintSource();
        if (parentT != null)
        {
            myConstraintSource.sourceTransform = parentT;
            myConstraintSource.weight = 1;
            processingEffectObj.GetPositionConstraint().SetSources(new List<ConstraintSource> { myConstraintSource });
            processingEffectObj.GetPositionConstraint().locked = true;
            processingEffectObj.GetPositionConstraint().translationOffset = Vector3.zero;
            processingEffectObj.GetPositionConstraint().constraintActive = true;
        }else{
            myConstraintSource.weight = 0;
            processingEffectObj.GetPositionConstraint().constraintActive = false;
        }
        processingEffectObj.transform.position = pos;
        processingEffectObj.transform.rotation = qua;
        return processingEffectObj;
    }
    
    private static readonly IDictionary<string, int> PreloadCountIncrementLog = new Dictionary<string, int>();
    static async UniTask<DecompositionPool> ConstructEffectPoolWithPrefabAndKey(GameObject prefab, string key, int iniCount)
    {
        if (EffectPoolsDic.ContainsKey(key))
        {
            DicAdd<string, int>.Add(PreloadCountIncrementLog, key, PreloadCountIncrementLog[key]+1);
            await EffectPoolsDic[key].PreloadAsync(PreloadCountIncrementLog[key], 1).ToUniTask();
            return EffectPoolsDic[key];
        }

        if (prefab != null)
        {
            var poolToConstruct = new DecompositionPool(prefab);
            await poolToConstruct.PreloadAsync(iniCount, 1).ToUniTask();
            if (EffectPoolsDic.ContainsKey(key))
            {
                poolToConstruct.Clear();
                return EffectPoolsDic[key];
            }

            EffectPoolsDic.Add(new KeyValuePair<string, DecompositionPool>(key, poolToConstruct));
            DicAdd<string, int>.Add(PreloadCountIncrementLog, key, iniCount);
            return poolToConstruct;
        }
        return null;
    }
    
    public static async UniTask<DecompositionPool> IniEffectsPool(string resourceName, string effectPath, int objectCount)
    {
        DecompositionPool effectPool;
        if (effectPath != null)
        {
            if (EffectPoolsDic.ContainsKey(effectPath + "/" + resourceName))
            {
                EffectPoolsDic.TryGetValue(effectPath + "/" + resourceName, out effectPool);
                if (effectPool != null)
                {
                    return effectPool;
                }
            }
            
            var effectPrefab = await TryLoadEffectPrefab(effectPath + "/" + resourceName + ".prefab");
            if (effectPrefab != null)
            {
                effectPool = await ConstructEffectPoolWithPrefabAndKey(effectPrefab, effectPath + "/" + resourceName, objectCount);
                return effectPool;
            }
            if (effectPath == FightGlobalSetting.EffectPathDefine())
            {
                return null;//防止无限循环
            }
        }
        effectPool = await IniEffectsPool(resourceName, FightGlobalSetting.EffectPathDefine(Element.Null), objectCount);
        return effectPool;
    }
}

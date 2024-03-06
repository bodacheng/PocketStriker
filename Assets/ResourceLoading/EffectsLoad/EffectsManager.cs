using UniRx;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class EffectsManager
{
    // 以下的重点是主界面和战斗界面通用问题
    static readonly IDictionary<string, DecompositionPool> EffectPoolsDic = new Dictionary<string, DecompositionPool>();
    static AsyncOperationHandle<GameObject> _handle;
    
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
    
    static DecompositionPool ConstructEffectPoolWithPrefabAndKey(GameObject prefab, string key, int iniCount)
    {
        var poolToConstruct = new DecompositionPool(prefab);
        poolToConstruct.PreloadAsync(iniCount, 1).Subscribe(_ => {});//Debug.Log("已经为对象池:"+key+"预留"+ini_count+"个物件")
        if (EffectPoolsDic.ContainsKey(key))
            EffectPoolsDic[key] = poolToConstruct;
        else
            EffectPoolsDic.Add(new KeyValuePair<string, DecompositionPool>(key, poolToConstruct));
        
        return poolToConstruct;
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
                    return effectPool;
            }
            
            var effectPrefab = await TryLoadEffectPrefab(effectPath + "/" + resourceName + ".prefab");
            if (effectPrefab != null)
            {
                effectPool = ConstructEffectPoolWithPrefabAndKey(effectPrefab, effectPath + "/" + resourceName, objectCount);
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
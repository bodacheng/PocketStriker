using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class HurtObjectManager
{
    static DecompositionPool _defaultHitBoxPool;
    static readonly IDictionary<string, DecompositionPool> HurtPoolDic = new Dictionary<string, DecompositionPool>();
    static AsyncOperationHandle<GameObject> _handle;
    
    static UniTask<GameObject> TryLoadWeaponPrefab(string key)
    {
        if (!AddressablesLogic.CheckKeyExist("weapon", key))
        {
            return default;
        }
        else
        {
            var returnValue =  AddressablesLogic.LoadT<GameObject>(key);
            return returnValue;
        }
    }
    
    public static DecompositionPool GetDPool()
    {
        return _defaultHitBoxPool;
    }
    
    public static void Clear()
    {
        foreach (var keyValuePair in HurtPoolDic)
        {
            keyValuePair.Value.Clear();
        }
        HurtPoolDic.Clear();
    }
    
    // 默认攻击物件池的创建
    public static async UniTask ConstructDPool()
    {
        _defaultHitBoxPool?.Clear();
        var resultObject = await AddressablesLogic.LoadT<GameObject>(FightGlobalSetting.EffectPathDefine() + "/dHitBox.prefab");
        if (resultObject == null)
        {
            return;
        }
        
        _defaultHitBoxPool = new DecompositionPool(resultObject);
        _defaultHitBoxPool.PreloadAsync(10, 1);
    }
    
    public static async UniTask ConstructHurtObjectPool(string resourceName, Element element, int preloadCount)
    {
        DecompositionPool poolToConstruct;
        GameObject weaponPrefab = null;
        
        ///////////////第二环节 ： 搜索属性魔法//////////////////
        string basicMagicForwardPath = FightGlobalSetting.EffectPathDefine(element);
        if (HurtPoolDic.ContainsKey(basicMagicForwardPath + "/" + resourceName))
        {
            HurtPoolDic.TryGetValue(basicMagicForwardPath + "/" + resourceName, out poolToConstruct);
            if (poolToConstruct != null)
                return;
        }
        
        weaponPrefab = await TryLoadWeaponPrefab(basicMagicForwardPath + "/" + resourceName + ".prefab");
        if (weaponPrefab != null)
        {
            poolToConstruct = ConstructHitBoxPoolWithPrefabAndKey(weaponPrefab, basicMagicForwardPath + "/" + resourceName, preloadCount);
            
            var decomposition = weaponPrefab.GetComponent<Decomposition>();
            if (decomposition != null)
            {
                if (decomposition.Attachments != null && decomposition.Attachments.Length > 0)
                {
                    for (var i = 0; i < decomposition.Attachments.Length; i++)
                    {
                        await ConstructHurtObjectPool(decomposition.Attachments[i], element, preloadCount);
                    }
                }
            }else{
                Debug.Log(resourceName + "没有Decompositioner！？");
            }
            return;
        }

        //////////// 第三环节  /////////////
        if (basicMagicForwardPath != FightGlobalSetting.EffectPathDefine())
        {
            basicMagicForwardPath = FightGlobalSetting.EffectPathDefine();
            if (HurtPoolDic.ContainsKey(basicMagicForwardPath + "/" + resourceName))
            {
                HurtPoolDic.TryGetValue(basicMagicForwardPath + "/" + resourceName, out poolToConstruct);
                if (poolToConstruct != null)
                {
                    return;
                }
            }
            
            weaponPrefab = await TryLoadWeaponPrefab(basicMagicForwardPath + "/" + resourceName + ".prefab");
            if (weaponPrefab != null)
            {
                poolToConstruct = ConstructHitBoxPoolWithPrefabAndKey(weaponPrefab, basicMagicForwardPath + "/" + resourceName, preloadCount);
                
                var d = weaponPrefab.GetComponent<Decomposition>();
                if (d != null)
                {
                    if (d.Attachments != null && d.Attachments.Length > 0)
                    {
                        for (int i = 0; i < d.Attachments.Length; i++)
                        {
                            await ConstructHurtObjectPool(d.Attachments[i], element, preloadCount);
                        }
                    }
                }else{
                    Debug.Log(resourceName + "没有Decompositioner！？");
                }
            }
        }
    }
    
    static DecompositionPool _hurtObjectPool;
    public static DecompositionPool GetHurtObjectPool(string resource_name, string myDefaultMagicPath)
    {
        _hurtObjectPool = null;
        
        // 第二轮
        if (HurtPoolDic.ContainsKey(myDefaultMagicPath + "/" + resource_name))
        {
            HurtPoolDic.TryGetValue(myDefaultMagicPath + "/" + resource_name, out _hurtObjectPool);
            if (_hurtObjectPool != null)
            {
                return _hurtObjectPool;
            }
        }
        
        // 第三轮
        if (myDefaultMagicPath != FightGlobalSetting.EffectPathDefine())
        {
            myDefaultMagicPath = FightGlobalSetting.EffectPathDefine();
            if (HurtPoolDic.ContainsKey(myDefaultMagicPath + "/" + resource_name))
            {
                HurtPoolDic.TryGetValue(myDefaultMagicPath + "/" + resource_name, out _hurtObjectPool);
                if (_hurtObjectPool != null)
                {
                    return _hurtObjectPool;
                }
            }
        }
        return null;
    }
    
    static DecompositionPool ConstructHitBoxPoolWithPrefabAndKey(GameObject prefab, string key, int iniCount)
    {
        if (prefab != null)
        {
            var poolToConstruct = new DecompositionPool(prefab);
            poolToConstruct.PreloadAsync(iniCount, 1).Subscribe(_ => {});
            DicAdd<string, DecompositionPool>.Add(HurtPoolDic, key, poolToConstruct);
            return poolToConstruct;
        }
        return null;
    }
}

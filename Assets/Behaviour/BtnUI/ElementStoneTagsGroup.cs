using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

//技能石盒分类系成员
public class ElementStoneTagsGroup
{
    IDictionary<int, ParticleSystem> _btnEffectsSetsForStoneBox = new Dictionary<int, ParticleSystem>();
    readonly IDictionary<int, ParticleSystem> _btnPressedEffects = new Dictionary<int, ParticleSystem>();
    readonly IDictionary<int, ParticleSystem> _exTagEffects = new Dictionary<int, ParticleSystem>();
    readonly IDictionary<int, ParticleSystem> _slotEffects = new Dictionary<int, ParticleSystem>();
    ParticleSystem _selectedTab;
    
    public void SetSelectedTabPos(int ex)
    {
        _selectedTab.Clear(true);
        _exTagEffects.TryGetValue(ex, out var tab);
        if (tab == null)
        {
            return;
        }
        _selectedTab.transform.SetParent(tab.transform);
        _selectedTab.transform.localPosition = Vector3.zero;
        _selectedTab.Play();
    }
    
    public void CloseTagEffects()
    {
        foreach(var keyValuePair in _btnEffectsSetsForStoneBox)
        {
            keyValuePair.Value.gameObject.SetActive(false);
            //keyValuePair.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach(var keyValuePair in _btnPressedEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach(var keyValuePair in _exTagEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach(var keyValuePair in _slotEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        _selectedTab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    
    public void OpenTagEffects()
    {
        foreach(var keyValuePair in _btnEffectsSetsForStoneBox)
        {
            if (keyValuePair.Value != null)
            {
                keyValuePair.Value.gameObject.SetActive(true);
                keyValuePair.Value.Play(true);
            }
        }
        foreach(var keyValuePair in _btnPressedEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Play(true);
        }
        foreach(var keyValuePair in _exTagEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Play(true);
        }
        foreach(var keyValuePair in _slotEffects)
        {
            if (keyValuePair.Value != null)
                keyValuePair.Value.Play(true);
        }
        _selectedTab.Play(true);
    }
    
    public async UniTask IniForSkillStoneBox(Element element, Transform effectObjectParent)
    {
        _btnEffectsSetsForStoneBox = new Dictionary<int, ParticleSystem>();
        
        var tasks = new[] {
            CreateOneButtonIcon(element, 0),
            CreateOneButtonIcon(element, 1),
            CreateOneButtonIcon(element, 2),
            CreateOneButtonIcon(element, 3)
        };
        
        var results = await UniTask.WhenAll(tasks);
        
        var normalTab = results[0];
        var ex1Tab = results[1];
        var ex2Tab = results[2];
        var ex3Tab = results[3];
        
        normalTab.transform.SetParent(effectObjectParent);
        ex1Tab.transform.SetParent(effectObjectParent);
        ex2Tab.transform.SetParent(effectObjectParent);
        ex3Tab.transform.SetParent(effectObjectParent);
        
        _btnEffectsSetsForStoneBox.Add(0, normalTab.GetComponent<ParticleSystem>());
        _btnEffectsSetsForStoneBox.Add(1, ex1Tab.GetComponent<ParticleSystem>());
        _btnEffectsSetsForStoneBox.Add(2, ex2Tab.GetComponent<ParticleSystem>());
        _btnEffectsSetsForStoneBox.Add(3, ex3Tab.GetComponent<ParticleSystem>());
        
        _selectedTab = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/selectedTab");
        _selectedTab.transform.SetParent(effectObjectParent);
        
        await LoadPressedEffect(element, effectObjectParent);
    }
    
    static UniTask<GameObject> CreateOneButtonIcon(Element element, int spLevel)
    {
        var path = FightGlobalSetting.EffectPathDefine(element);
        switch(spLevel)
        {
            case 0:
                return AddressablesLogic.LoadObject("ButtonEffects/" + path + "/normal.prefab");
            case 1:
                return AddressablesLogic.LoadObject("ButtonEffects/" + path + "/EX1.prefab");
            case 2:
                return AddressablesLogic.LoadObject("ButtonEffects/" + path + "/EX2.prefab");
            case 3:
                return AddressablesLogic.LoadObject("ButtonEffects/" + path + "/EX3.prefab");
            default:
                return default;
        }
    }
    
    async UniTask LoadPressedEffect(Element element, Transform T)
    {
        var path = FightGlobalSetting.EffectPathDefine(element);
        
        var triggerExplosion0 = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion0.prefab");
        var triggerExplosion1 = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion1.prefab");
        var triggerExplosion2 = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion2.prefab");
        var triggerExplosion3 = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion3.prefab");
        
        triggerExplosion0.transform.SetParent(T);
        triggerExplosion1.transform.SetParent(T);
        triggerExplosion2.transform.SetParent(T);
        triggerExplosion3.transform.SetParent(T);
        
        _btnPressedEffects.Add(0, triggerExplosion0);
        _btnPressedEffects.Add(1, triggerExplosion1);
        _btnPressedEffects.Add(2, triggerExplosion2);
        _btnPressedEffects.Add(3, triggerExplosion3);
    }
    
    public void RefreshBoxEffects(int eX, Vector3 pos)
    {
        if (_exTagEffects.ContainsKey(eX))
            return;
        var p = _btnEffectsSetsForStoneBox[eX];
        p.gameObject.name = "UIExTag"+ eX;
        _exTagEffects.Add(eX,p);
        p.gameObject.transform.position = pos;
        p.gameObject.SetActive(true);
        p.Play(true);
    }
    
    public async void RefreshSlotEffects(int slotNum, int eX, Vector3 pos, Transform releaseTarget)
    {
        if (_slotEffects.ContainsKey(slotNum) && _slotEffects[slotNum] != null)
        {
            Object.Destroy(_slotEffects[slotNum].gameObject);
        }
        
        if (!_btnEffectsSetsForStoneBox.ContainsKey(eX)) return;
        string effectName;
        switch (eX)
        {
            case 1:
                effectName = "SlotEffects/ex1";
                break;
            case 2:
                effectName = "SlotEffects/ex2";
                break;
            case 3:
                effectName = "SlotEffects/ex3";
                break;
            default:
                effectName = "SlotEffects/normal";
                break;
        }
        var slotEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>(effectName, releaseTarget.gameObject);
        
        slotEffect.transform.localScale = 
            new Vector3(slotEffect.transform.localScale.x * PosCal.TempRate(),
                slotEffect.transform.localScale.y * PosCal.TempRate(),
                slotEffect.transform.localScale.z);
        
        //slotEffect.transform.SetParent(parent);
        DicAdd<int, ParticleSystem>.Add(_slotEffects, slotNum, slotEffect);
        slotEffect.gameObject.name = "slotEffect"+ slotNum;
        slotEffect.gameObject.transform.position = pos;
        slotEffect.Play(true);
    }
    
    public void Clear()
    {
        foreach (var variable in _exTagEffects)
        {
            if (variable.Value != null)
                GameObject.Destroy(variable.Value.gameObject);
        }
        _exTagEffects.Clear();
        
        foreach (var variable in _btnPressedEffects)
        {
            if (variable.Value != null)
                GameObject.Destroy(variable.Value.gameObject);
        }
        _btnPressedEffects.Clear();
        
        foreach (var variable in _btnEffectsSetsForStoneBox)
        {
            if (variable.Value != null)
                GameObject.Destroy(variable.Value.gameObject);
        }
        _btnEffectsSetsForStoneBox.Clear();
        
        foreach (var variable in _slotEffects)
        {
            if (variable.Value != null)
                GameObject.Destroy(variable.Value.gameObject);
        }
        _slotEffects.Clear();
        
        GameObject.Destroy(_selectedTab.gameObject);
    }

    public void SkillButtonExplosion(int spLevel, Vector3 targetPos, Transform parent)
    {
        var pressedExplosion = _btnPressedEffects[spLevel];
        pressedExplosion.gameObject.name = "ExExplosion" + spLevel;
        pressedExplosion.transform.position = targetPos;
        pressedExplosion.Play();
        pressedExplosion.transform.SetParent(parent);
    }
}

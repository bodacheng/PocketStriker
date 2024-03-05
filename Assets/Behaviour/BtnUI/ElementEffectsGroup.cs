using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ElementEffectsGroup
{
    IDictionary<Button, ParticleSystem> _btnRefreshEffects = new Dictionary<Button, ParticleSystem>();
    ParticleSystem _triggerExplosion0;
    ParticleSystem _triggerExplosion1;
    ParticleSystem _triggerExplosion2;
    ParticleSystem _triggerExplosion3;
    IDictionary<Button, ParticleSystem> _buttonSlotEffects;
    ParticleSystem _defendBtn;
    ParticleSystem _rushBtn;
    ParticleSystem _dreamComboBtn;
    ParticleSystem _pressingExplosion; // 这个不需要对象池。

    public void DreamComboEffectOn(bool on)
    {
        if (on)
        {
            _dreamComboBtn.Play(true);
        }
        else
        {
            _dreamComboBtn.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    
    public void StartPressing(Button targetBtn)
    {
        var targetPos = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, targetBtn.GetComponent<RectTransform>(), 7);
        _pressingExplosion.transform.position = targetPos;
        _pressingExplosion.Play();
    }
    
    public void StopPressing()
    {
        _pressingExplosion.Stop();
    }
    
    public void BtnRefreshEffect()
    {
        foreach (var pair in _btnRefreshEffects)
        {
            pair.Value.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, pair.Key.GetComponent<RectTransform>(),4);
            pair.Value.Play(true);
        }
    }

    public ParticleSystem GetExplosionEffect(int spLevel)
    {
        ParticleSystem targetExplode;
        switch(spLevel)
        {
            case 0:
                targetExplode = _triggerExplosion0;
                break;
            case 1:
                targetExplode = _triggerExplosion1;
                break;
            case 2:
                targetExplode = _triggerExplosion2;
                break;
            case 3:
                targetExplode = _triggerExplosion3;
                break;
            default:
                return null;
        }
        return targetExplode;
    }
    
    public void Close(ParticleSystemStopBehavior systemStopBehavior)
    {
        foreach (var kv in _buttonSlotEffects)
        {
            kv.Value.Stop(true, systemStopBehavior);
        }
        
        _triggerExplosion0.Stop(true, systemStopBehavior);
        _triggerExplosion1.Stop(true, systemStopBehavior);
        _triggerExplosion2.Stop(true, systemStopBehavior);
        _triggerExplosion3.Stop(true, systemStopBehavior);
        
        foreach (var keyValue in _btnRefreshEffects)
        {
            keyValue.Value.Stop(true, systemStopBehavior);
        }
        _pressingExplosion.Stop(true, systemStopBehavior);
        _rushBtn.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _dreamComboBtn.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        if (FightGlobalSetting.HasDefend)
            _defendBtn.Stop(true, systemStopBehavior);
    }
    
    public void Open(Vector3 defendBtnPos, Vector3 rushBtnPos, Vector3 dreamComboPos)
    {
        _triggerExplosion0.Stop(true);
        _triggerExplosion1.Stop(true);
        _triggerExplosion2.Stop(true);
        _triggerExplosion3.Stop(true);
        
        foreach (var keyValue in _btnRefreshEffects)
        {
            keyValue.Value.Stop(true);
        }
        _pressingExplosion.Stop(true);
        _rushBtn.gameObject.transform.position = rushBtnPos;
        _rushBtn.Play(true);

        _dreamComboBtn.transform.position = dreamComboPos;
        
        if (FightGlobalSetting.HasDefend)
        {
            _defendBtn.gameObject.transform.position = defendBtnPos;
            _defendBtn.Play(true);
        }
    }

    public async UniTask InitializeCommon(Transform targetRectT, Element element, Button a1Btn, Button a2Btn, Button a3Btn)
    {
        var path = FightGlobalSetting.EffectPathDefine(element);
        
        var tasks = new List<UniTask<ParticleSystem>> 
        {
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/slot.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/slot.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/slot.prefab")
        };
        var results = await UniTask.WhenAll(tasks);
        var attackSlot = results[0];
        var fire1Slot = results[1];
        var fire2Slot = results[2];
        
        attackSlot.transform.SetParent(targetRectT);
        fire1Slot.transform.SetParent(targetRectT);
        fire2Slot.transform.SetParent(targetRectT);
        
        _buttonSlotEffects = new Dictionary<Button, ParticleSystem>
        {
            { a1Btn, attackSlot },
            { a2Btn, fire1Slot },
            { a3Btn, fire2Slot }
        };

        if (FightGlobalSetting.HasDefend)
        {
            _defendBtn = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/defend.prefab");
        }
        
        var tasks2 = new List<UniTask<ParticleSystem>> 
        {
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/rush.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/refresh.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/refresh.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/refresh.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion0.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion1.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion2.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/explosion3.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/pressing.prefab"),
            AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/" + path + "/dreamCombo.prefab")
        };

        var results2 = await UniTask.WhenAll(tasks2);

        _rushBtn = results2[0];
        var a1Refresh = results2[1];
        var a2Refresh = results2[2];
        var a3Refresh = results2[3];
        _triggerExplosion0 = results2[4];
        _triggerExplosion1 = results2[5];
        _triggerExplosion2 = results2[6];
        _triggerExplosion3 = results2[7];
        _pressingExplosion = results2[8];
        _dreamComboBtn = results2[9];
        
        _btnRefreshEffects = new Dictionary<Button, ParticleSystem>
        {
            { a1Btn, a1Refresh },
            { a2Btn, a2Refresh },
            { a3Btn, a3Refresh }
        };
    }
    
    public void RefreshSlotEffect(Button button, string skillId, Vector3 pos)
    {
        if (skillId == String.Empty)
        {
            _buttonSlotEffects[button].transform.position = pos;
            _buttonSlotEffects[button].Play(true);
        }else{
            _buttonSlotEffects[button].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

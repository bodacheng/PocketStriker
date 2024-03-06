using System;
using UnityEngine;
using UnityEngine.UI;

// 战斗暂停相关。从暂停界面可以跳转至Setting界面，因此两个模块靠OptionsButton连接在一起
public class FightScenePauseSupport : UILayer
{
    private Action resumeAction;
    private Action returnAction;
    
    #region Sound
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider effectsSoundsSlider;
    #endregion
    
    public void Setup(Action runNow, Action returnToFront, Action resumeAction)
    {
        this.resumeAction = resumeAction;
        this.returnAction = returnToFront;
        runNow.Invoke();
        ResetSliders();
    }

    public void Resume()
    {
        resumeAction.Invoke();
    }

    public void Return()
    {
        returnAction.Invoke();
    }
    
    void ResetSliders()
    {
        effectsSoundsSlider.value = AppSetting.Value.EffectsVolume;
        bgmSlider.value = AppSetting.Value.BgmVolume;
    }
    
    public void OnBgmChange()
    {
        AppSetting.Value.BgmVolume = bgmSlider.value;
    }
    
    public void OnEffectChange()
    {
        AppSetting.Value.EffectsVolume = effectsSoundsSlider.value;
    }
}

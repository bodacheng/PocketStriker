using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using DummyLayerSystem;
using FightScene;
using UnityEngine;
using UnityEngine.UI;
using Soul;
using Skill;
using UniRx;
using RengeGames.HealthBars;

public enum InputKey
{
    Null = -1,
    Attack1 = 0,
    Attack2 = 1,
    Attack3 = 2,
    Acc = 5,
    Defend = 3,
    Defend_Cancel = 4,
    DreamCombo = 6,
    Any = 6
}

public class MobileInputsManager : MonoBehaviour {

    [SerializeField] BOButton a1Btn;
    [SerializeField] BOButton a2Btn;
    [SerializeField] BOButton a3Btn;
    [SerializeField] BOButton defendBtn;
    [SerializeField] BOButton dashBtn;
    [SerializeField] BOButton dreamComboBtn;
    [SerializeField] UltimateJoystick joystick;
    [SerializeField] Transform effectsParent;
    [SerializeField] RadialSegmentedHealthBar radialSegmentedHealthBar;
    [SerializeField] float buttonStretchEdgeDis = 5f;
    
    //攻击键系成员
    readonly IDictionary<string, GameObject> _aIcons = new Dictionary<string, GameObject>();
    readonly IDictionary<string, GameObject> _bIcons = new Dictionary<string, GameObject>();
    readonly IDictionary<string, GameObject> _cIcons = new Dictionary<string, GameObject>();
    IDictionary<Button, IDictionary<string, GameObject>> btnIcons = new Dictionary<Button, IDictionary<string, GameObject>>();
    readonly IDictionary<Element, ElementEffectsGroup> _elementEffects = new Dictionary<Element, ElementEffectsGroup>();
    Element _focusing;
    public BOButton DreamComboBtn => dreamComboBtn;

    async UniTask AddGemIcon(string skillID, IDictionary<string, GameObject> dic, Button btn)
    {
        if (!dic.ContainsKey(skillID))
        {
            var icon = await Stones.GenerateStoneModel(skillID, false);
            if (icon == null) return;
            if (dic.ContainsKey(skillID))
            {
                Destroy(icon.gameObject);
                return;
            }
            DicAdd<string, GameObject>.Add(dic, skillID, icon.gameObject);
            Parent(icon.transform, btn.transform, buttonStretchEdgeDis);
        }
        
        void Parent(Transform t, Transform target, float edgeDis)
        {
            t.SetParent(target);
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
            
            var rectTransform = t.GetComponent<RectTransform>();

            // 设置四个方向的offset为0
            rectTransform.offsetMin = new Vector2(edgeDis, edgeDis); // Left, Bottom
            rectTransform.offsetMax = new Vector2(-edgeDis, -edgeDis); // Right, Top

            // 设置锚点为中心点
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            
            // 设置轴心点为中心
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            t.gameObject.SetActive(false);
        }
    }

    public void PreparingMode(bool preparingMode)
    {
        a1Btn.interactable = !preparingMode;
        a2Btn.interactable = !preparingMode;
        a3Btn.interactable = !preparingMode;
        dashBtn.interactable = !preparingMode;
        dreamComboBtn.interactable = !preparingMode;
        joystick.enabled = !preparingMode;
    }
    
    bool _inputting = false;
    public bool Inputting => _inputting;
    
    private readonly ReactiveProperty<Data_Center> focus = new ReactiveProperty<Data_Center>();
    public ReactiveProperty<Data_Center> CurrentFocus => focus;

    private IDisposable watchDreamComboGauge;
    public void FocusUnit(Data_Center center, bool force = false)
    {
        if (focus.Value != null)
        {
            focus.Value._MyBehaviorRunner.InputsManager = null;
        }
        if (center != null)
        {
            center._MyBehaviorRunner.InputsManager = this;
            focus.Value = center;
            SwitchElementEffects(center.element);
            SuddenRefreshButtons(focus.Value._MyBehaviorRunner, force);
            watchDreamComboGauge?.Dispose();
            watchDreamComboGauge = center.FightDataRef.DreamComboGauge.Subscribe(
                (x) =>
                {
                    var percent = (float)x / FightGlobalSetting._DreamComboGaugeMax;
                    if (percent == 1)
                    {
                        DreamComboEffectOn();
                    }
                    else
                    {
                        DreamComboEffectOff();
                    }
                    radialSegmentedHealthBar.SetPercent(percent);
                }
            ).AddTo(this.gameObject);
            
            if (FightLoad.Fight.RunTutorial)
            {
                IDisposable dreamComboIntro = null;
                dreamComboIntro = center.FightDataRef.DreamComboGauge.Subscribe(
                    (x) =>
                    {
                        var percent = (float)x / FightGlobalSetting._DreamComboGaugeMax;
                        if (percent == 1)
                        {
                            var _layer = UILayerLoader.Get<FightingStepLayer>();
                            _layer.ForceClickDreamComboBtn();
                            _layer.Team1UI.AutoSwitch.ChangeAutoState(false);
                            _layer.Team2UI.AutoSwitch.ChangeAutoState(false);
                            dreamComboIntro?.Dispose();
                        }
                    }
                ).AddTo(this.gameObject);
            }
            TurnOnButtons();
        }
        else
        {
            focus.Value = null;
            TurnOffButtons();
        }
    }
    
    public void Clear()
    {
        _elementEffects.Clear();
        foreach (var kv in btnIcons)
        {
            foreach (var kvv in kv.Value)
            {
                Destroy(kvv.Value);
            }
        }
        btnIcons.Clear();
        _aIcons.Clear();
        _bIcons.Clear();
        _cIcons.Clear();
        Destroy(gameObject);
    }
    
    // 切换输入按键表现层（红黄蓝绿）.这个函数使用的前提是所有用的上的控制器组都已经注册并初始化
    void SwitchElementEffects(Element element)
    {
        if (_elementEffects.ContainsKey(_focusing))
        {
            _elementEffects[_focusing].Close(ParticleSystemStopBehavior.StopEmitting);
        }
        
        if (_elementEffects.ContainsKey(element))
        {
            _focusing = element;
            _elementEffects[element].Open(
                PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, defendBtn.GetComponent<RectTransform>(), 5), 
                PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, dashBtn.GetComponent<RectTransform>(), 5),
                PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, dreamComboBtn.GetComponent<RectTransform>(), 5)
            );
        }else{
            Debug.Log("检查手机控制器渲染模块加载顺序");
        }
    }
    
    public async UniTask ElementRegister(Element element, UnitInfo unitInfo)
    {
        if (!_elementEffects.ContainsKey(element))
        {
            var elementEffect = new ElementEffectsGroup();
            await elementEffect.InitializeCommon(effectsParent, element, a1Btn, a2Btn, a3Btn);
            DicAdd<Element,ElementEffectsGroup>.Add(_elementEffects, element, elementEffect);
        }
        
        await UniTask.WhenAll(
            AddGemIcon(unitInfo.set.a1, _aIcons, a1Btn),
            AddGemIcon(unitInfo.set.a2, _aIcons, a1Btn),
            AddGemIcon(unitInfo.set.a3, _aIcons, a1Btn),
            AddGemIcon(unitInfo.set.b1, _bIcons, a2Btn),
            AddGemIcon(unitInfo.set.b2, _bIcons, a2Btn),
            AddGemIcon(unitInfo.set.b3, _bIcons, a2Btn),
            AddGemIcon(unitInfo.set.c1, _cIcons, a3Btn),
            AddGemIcon(unitInfo.set.c2, _cIcons, a3Btn),
            AddGemIcon(unitInfo.set.c3, _cIcons, a3Btn)
        );
        
        _elementEffects[element].Close(ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void GroupSkillIcons()
    {
        btnIcons = new Dictionary<Button, IDictionary<string, GameObject>>
        {
            { a1Btn, _aIcons },
            { a2Btn, _bIcons },
            { a3Btn, _cIcons }
        };
    }
    
    public void SkillExplosion(InputKey key, int spLevel)
    {
        if (!_elementEffects.ContainsKey(_focusing))
        {
            Debug.Log("读取流程产生错误："+_focusing);
            return;
        }
        var targetExplode = _elementEffects[_focusing].GetExplosionEffect(spLevel);
        switch (key)
        {
            case InputKey.Attack1:
                targetExplode.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, a1Btn.GetComponent<RectTransform>(), 3);
                break;
            case InputKey.Attack2:
                targetExplode.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, a2Btn.GetComponent<RectTransform>(), 3);
                break;
            case InputKey.Attack3:
                targetExplode.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, a3Btn.GetComponent<RectTransform>(), 3);
                break;
            case InputKey.DreamCombo:
                targetExplode.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, dreamComboBtn.GetComponent<RectTransform>(), 3);
                break;
        }
        targetExplode?.Play();
    }

    //下面这些是说，每当有技能爆炸特效也就代表技能表更新，那么需要整体刷新特效 刷新特效都是三个键位一起出现，省的给人种误导好像我技能没变
    public void BtnRefreshFrames()
    {
        _elementEffects[_focusing].BtnRefreshEffect();
    }
    
    // 等把机动和防御分离后，要做这样的事情：
    // 根据玩家的技能列表来决定防御，机动，三攻击键分别存在与否。
    // 然后，refresh button是要看情况的，攻击键要么是变成空按钮，要么应该是就没有按钮。。。？
    // 而防御与机动则是确定一直显示。
    void StartPressing(Button targetBtn)
    {
        if (_elementEffects.ContainsKey(_focusing))
            _elementEffects[_focusing].StartPressing(targetBtn);
    }

    void StopPressing()
    {
        if (_elementEffects.ContainsKey(_focusing))
            _elementEffects[_focusing].StopPressing();
    }

    void DreamComboEffectOn()
    {
        if (_elementEffects.ContainsKey(_focusing))
            _elementEffects[_focusing].DreamComboEffectOn(true);
    }
    
    void DreamComboEffectOff()
    {
        if (_elementEffects.ContainsKey(_focusing))
            _elementEffects[_focusing].DreamComboEffectOn(false);
    }
    
    // 如果不是对准角色，不会跑。
    static float h;
    static float v;
    void CheckIfPlayerIsInputting()
    {
        _inputting = defendButtonHover || attack || fire1 || fire2;
        if (_inputting)
        {
            return;
        }
        h = UnityEngine.Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("joystick");
        v = UnityEngine.Input.GetAxis("Vertical") + UltimateJoystick.GetVerticalAxis("joystick");
        _inputting = (h > 0f || h < 0 || v > 0f || v < 0f);
    }
    
    readonly Dictionary<InputKey, SkillEntity> _optionsLastFrame = new Dictionary<InputKey, SkillEntity>()
    {
        {InputKey.Attack1,null},
        {InputKey.Attack2,null},
        {InputKey.Attack3,null}
    };
    
    // 动态按钮系统是基于状态流动
    SkillEntity _behaviorPreviewButton1, _behaviorPreviewButton2, _behaviorPreviewButton3;
    public void ButtonsFeatureLoad(List<SkillEntity> optionsPreview, bool force = false)
    {
        _behaviorPreviewButton1 = null; 
        _behaviorPreviewButton2 = null;
        _behaviorPreviewButton3 = null;
        
        for (var i = 0; i < optionsPreview.Count; i++)
        {
            switch (optionsPreview[i].EnterInput)
            {
                case InputKey.Attack1:
                    _behaviorPreviewButton1 = optionsPreview[i];
                    break;
                case InputKey.Attack2:
                    _behaviorPreviewButton2 = optionsPreview[i];
                    break;
                case InputKey.Attack3:
                    _behaviorPreviewButton3 = optionsPreview[i];
                    break;
            }
        }
        
        if (_optionsLastFrame[InputKey.Attack1] != _behaviorPreviewButton1 || force)
        {
            RefreshPattern(a1Btn, _behaviorPreviewButton1 != null ? _behaviorPreviewButton1.SkillID : string.Empty);
        }
        if (_optionsLastFrame[InputKey.Attack2] != _behaviorPreviewButton2 || force)
        {
            RefreshPattern(a2Btn, _behaviorPreviewButton2 != null ? _behaviorPreviewButton2.SkillID : string.Empty);
        }
        if (_optionsLastFrame[InputKey.Attack3] != _behaviorPreviewButton3 || force)
        {
            RefreshPattern(a3Btn, _behaviorPreviewButton3 != null ? _behaviorPreviewButton3.SkillID : string.Empty);
        }
        
        _optionsLastFrame[InputKey.Attack1] = _behaviorPreviewButton1;
        _optionsLastFrame[InputKey.Attack2] = _behaviorPreviewButton2;
        _optionsLastFrame[InputKey.Attack3] = _behaviorPreviewButton3;
    }
    
    // 直接根据角色状态刷新按钮。因为动态按钮系统是基于状态流动
    void SuddenRefreshButtons(BehaviorRunner behaviorRunner, bool force = false)
    {
        _optionsLastFrame[InputKey.Attack1] = null;
        _optionsLastFrame[InputKey.Attack2] = null;
        _optionsLastFrame[InputKey.Attack3] = null;
        
        ButtonsFeatureLoad(behaviorRunner.GetNextSkills(), force);
    }
    
    public bool defendButtonHover;
    public bool DefendExitTrigger()
    {
        return !defendButtonHover;
    }
    
    public bool attack;
    public void AttackDown()
    {
        StartPressing(a1Btn);
        attack = true;
    }
    public void AttackUp()
    {
        StopPressing();
        attack = false;
    }
    
    public bool fire1;
    public void Fire1Down()
    {
        fire1 = true;
        StartPressing(a2Btn);
    }
    public void Fire1Up()
    {
        fire1 = false;
        StopPressing();
    }
    
    public bool fire2;
    public void Fire2Down()
    {
        fire2 = true;
        StartPressing(a3Btn);
    }
    public void Fire2Up()
    {
        fire2 = false;
        StopPressing();
    }
    
    public void DefendDown()
    {
        defendButtonHover = true;
        StartPressing(defendBtn);
    }
    public void DefendUp()
    {
        defendButtonHover = false;
        StopPressing();
    }
    
    public bool acc;
    public void RushDown()
    {
        acc = true;
        StartPressing(dashBtn);
    }
    public void RushUp()
    {
        acc = false;
        StopPressing();
    }

    public bool dreamCombo;
    public void DreamComboDown()
    {
        dreamCombo = true;
        StartPressing(dreamComboBtn);
    }
    
    public void DreamComboUp()
    {
        dreamCombo = false;
        StopPressing();
    }
    
    void TurnOnButtons()
    {
        a1Btn.gameObject.SetActive(true);
        a2Btn.gameObject.SetActive(true);
        a3Btn.gameObject.SetActive(true);
        dashBtn.gameObject.SetActive(true);
        dreamComboBtn.gameObject.SetActive(true);
        attack = false;
        fire1 = false;
        fire2 = false;
        acc = false;
        joystick.gameObject.SetActive(true);
        if (FightGlobalSetting.HasDefend)
        {
            defendBtn.gameObject.SetActive(true);
            defendButtonHover = false;
        }
    }

    void TurnOffButtons()
    {
        a1Btn.gameObject.SetActive(false);
        a2Btn.gameObject.SetActive(false);
        a3Btn.gameObject.SetActive(false);
        dashBtn.gameObject.SetActive(false);
        dreamComboBtn.gameObject.SetActive(false);
        
        attack = false;
        fire1 = false;
        fire2 = false;
        acc = false;
        joystick.gameObject.SetActive(false);
        if (FightGlobalSetting.HasDefend)
        {
            defendBtn.gameObject.SetActive(false);
            defendButtonHover = false;
        }

        if (_elementEffects.ContainsKey(_focusing))
        {
            _elementEffects[_focusing].Close(ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        foreach(var kv in btnIcons)
        {
            foreach(var pair in kv.Value)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }
    }
    
    void Update()
    {
        CheckIfPlayerIsInputting();
    }
    
    void RefreshPattern(Button button, string skillId) //按钮切换也可以在这里做文章
    {
        Vector3 targetPos = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, button.GetComponent<RectTransform>(), 5);
        if (btnIcons.ContainsKey(button))
        {
            var target = btnIcons[button];
            foreach(var pair in target)
            {
                pair.Value.gameObject.SetActive(pair.Key == skillId);
            }
        }
        else
        {
            Debug.Log("战斗按键指示器逻辑错误："+ button);
        }
        
        if (_elementEffects.ContainsKey(_focusing))
        {
            _elementEffects[_focusing].RefreshSlotEffect(button, skillId, targetPos);
        }
        else
        {
            Debug.Log("error button effect："+　skillId);
        }
    }

    //void changeButtonPatternParticleVer(Button button,EX sp_level)//按钮切换也可以在这里做文章
    //{
    //    targetButtonPos = ButtonEffectInFxCameraWorldSpace(button);
        
    //    GameObject refresh_Explosion = _focusingButtonEffectsGroup.refreshPool.TryGetNextObject(button.transform.position, Quaternion.identity);
    //    refresh_Explosion.SetActive(true);
    //    refresh_Explosion.transform.position = targetButtonPos;
        
    //    GameObject EffectICon = null;
    //    switch (sp_level)
    //    {
    //        case EX.normal:
    //            EffectICon = _focusingButtonEffectsGroup.normalPool.TryGetNextObject(button.transform.position, Quaternion.identity);

    //            if (EffectICon != null)
    //                EffectICon.SetActive(true);
    //            else
    //            {
    //                Debug.Log("特效物体丢失");
    //                return;
    //            }
    //            EffectICon.transform.position = targetButtonPos;
    //            break;
    //        case EX.EX1:
    //            EffectICon = _focusingButtonEffectsGroup.EX1Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

    //            if (EffectICon != null)
    //                EffectICon.SetActive(true);
    //            else
    //            {
    //                Debug.Log("特效物体丢失");
    //                return;
    //            }

    //            EffectICon.transform.position = targetButtonPos;
    //            break;
    //        case EX.EX2:
    //            EffectICon = _focusingButtonEffectsGroup.EX2Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

    //            if (EffectICon != null)
    //                EffectICon.SetActive(true);
    //            else
    //            {
    //                Debug.Log("特效物体丢失");
    //                return;
    //            }
    //            EffectICon.transform.position = targetButtonPos;
    //            break;
    //        case EX.EX3:
    //            EffectICon = _focusingButtonEffectsGroup.EX3Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

    //            if (EffectICon != null)
    //                EffectICon.SetActive(true);
    //            else
    //            {
    //                Debug.Log("特效物体丢失");
    //                return;
    //            }
    //            EffectICon.transform.position = targetButtonPos;
    //            break;
    //        case EX.NULL:
    //            break;
    //    }
    //}    
}

    //void Awake()
    //{
        //normal.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //EX1.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //EX2.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //EX3.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //pressedExplosion.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //refreshExplosion.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
        //pressingExplosion.GetComponent<RectTransform>().sizeDelta = Attack.GetComponent<RectTransform>().sizeDelta;
    //}

    //void changeButtonPattern(Button button,EX sp_level)//按钮切换也可以在这里做文章
    //{
        //GameObject refresh_Explosion = refreshExplosionPool.TryGetNextObject(button.transform.position, Quaternion.identity);
        //refresh_Explosion.SetActive(true);
        //refresh_Explosion.transform.SetParent(button.transform);
        //refresh_Explosion.transform.SetSiblingIndex(2);
        //refresh_Explosion.GetComponent<RectTransform>().anchoredPosition = new Vector3(0,0,-1f);

        //if (SKillIcons[button] != null)
        //{
        //    SKillIcons[button].SetActive(false);
        //}

        //GameObject EffectICon = null;
        //switch (sp_level)
        //{
        //    case EX.normal:
        //        EffectICon = normalPool.TryGetNextObject(button.transform.position, Quaternion.identity);

        //        if (EffectICon != null)
        //            EffectICon.SetActive(true);
        //        else
        //        {
        //            Debug.Log("特效物体丢失");
        //            return;
        //        }

        //        EffectICon.transform.SetParent(button.transform);
        //        EffectICon.transform.SetSiblingIndex(1);
        //        EffectICon.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

        //        break;
        //    case EX.EX1:
        //        EffectICon = EX1Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

        //        if (EffectICon != null)
        //            EffectICon.SetActive(true);
        //        else
        //        {
        //            Debug.Log("特效物体丢失");
        //            return;
        //        }

        //        EffectICon.transform.SetParent(button.transform);
        //        EffectICon.transform.SetSiblingIndex(1);
        //        EffectICon.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        //        break;
        //    case EX.EX2:
        //        EffectICon = EX2Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

        //        if (EffectICon != null)
        //            EffectICon.SetActive(true);
        //        else
        //        {
        //            Debug.Log("特效物体丢失");
        //            return;
        //        }

        //        EffectICon.transform.SetParent(button.transform);
        //        EffectICon.transform.SetSiblingIndex(1);
        //        EffectICon.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        //        break;
        //    case EX.EX3:
        //        EffectICon = EX3Pool.TryGetNextObject(button.transform.position, Quaternion.identity);

        //        if (EffectICon != null)
        //            EffectICon.SetActive(true);
        //        else
        //        {
        //            Debug.Log("特效物体丢失");
        //            return;
        //        }

        //        EffectICon.transform.SetParent(button.transform);
        //        EffectICon.transform.SetSiblingIndex(1);
        //        EffectICon.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        //        break;
        //    case EX.NULL:
        //        break;
        //}

        //SKillIcons[button] = EffectICon;

        // 其实下面这些不会在运行了，因为现在所有的气力不足都是在上面的EX.Null case 里
        //if (hasPlentyGauge(sp_level))
        //{
        //    button.normalColor.a = 1f;
        //    button.pressedColor.a = 1f;
        //}else{
        //    button.pressedSprite = button.normalSprite;
        //    button.normalColor.a = 0.5f;
        //    button.pressedColor.a = 0.5f;
        //}
    //}

//底下这些成了我们开发以来最可笑的笑话之一，证明实在应该早睡否则脑子会混乱
//class inputAdvance
//{
//    public mobileInputsManager _mobileInputsManager;
//    public int inAdvanceFrames = 10;
//    int counter = 0;

//    public inputAdvance(mobileInputsManager _mobileInputsManager, int inAdvanceFrames)
//    {
//        this._mobileInputsManager = _mobileInputsManager;
//        this.inAdvanceFrames = inAdvanceFrames;
//        this.counter = 0;
//        this.nextInput = inputs_defined.Null;
//    }

//    public inputs_defined nextInput;

//    public void update()
//    {
//        if (nextInput != inputs_defined.Null)
//        {
//            counter++;
//            if (counter > inAdvanceFrames)
//            {
//                switch (nextInput)
//                {
//                    case inputs_defined.Attack:
//                        _mobileInputsManager.attackButtonUp();
//                        break;
//                    case inputs_defined.Fire1:
//                        _mobileInputsManager.Fire1ButtonUp();
//                        break;
//                    case inputs_defined.Fire2:
//                        _mobileInputsManager.Fire2ButtonUp();
//                        break;
//                }
//                nextInput = inputs_defined.Null;
//                counter = 0;
//            }else{
//                switch (nextInput)
//                {
//                    case inputs_defined.Attack:
//                        _mobileInputsManager.attackButtonDown();
//                        _mobileInputsManager.Fire1ButtonUp();
//                        _mobileInputsManager.Fire2ButtonUp();
//                        break;
//                    case inputs_defined.Fire1:
//                        _mobileInputsManager.Fire1ButtonDown();
//                        _mobileInputsManager.attackButtonUp();
//                        _mobileInputsManager.Fire2ButtonUp();
//                        break;
//                    case inputs_defined.Fire2:
//                        _mobileInputsManager.Fire2ButtonDown();
//                        _mobileInputsManager.attackButtonUp();
//                        _mobileInputsManager.Fire1ButtonUp();
//                        break;
//                }
//            }
//        }
//    }

//    public void clear()
//    {
//        nextInput = inputs_defined.Null;
//        counter = 0;
//    }

//    public void inputNextInAdvance(inputs_defined nextInput)
//    {
//        counter = 0;
//        this.nextInput = nextInput;
//    }
//}
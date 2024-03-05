using System;
using System.Collections.Generic;
using System.Threading;
using mainMenu;
using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using ModelView;
using Button = UnityEngine.UI.Button;

public partial class SkillEditLayer : UILayer
{
    [SerializeField] DedicatedCameraConnector camConnector;
    
    public DedicatedCameraConnector CamConnector=>camConnector;
    
    [Header("九宫格")]
    public TheNineSlot nineSlot;
    
    [Header("技能石盒")]
    public SkillStonesBox stonesBox;
    [SerializeField] RectTransform stoneBoxRect;
    
    [Header("技能信息")]
    [SerializeField] SkillStoneDetail skillStoneDetail;
    
    [Header("技能展示器模式切换角色按钮")]
    [SerializeField] Button unitSwitcher;
    
    [Header("Tutorial")]
    [SerializeField] ClickNextTutorial clickNextTutorial1, clickNextTutorial2;
    [SerializeField] GameObject mask;
    
    public bool Initialized { get; set; } = false;
    
    // For Transition Effects
    private readonly List<Tween> _tweens = new List<Tween>();
    private readonly List<GameObject> _transitionEffects = new List<GameObject>();
    
    public async UniTask ShowCombo(bool dreamCombo)
    {
        stonesBox._tabEffects.TurnShowingTagEffects(false);
        
        nineSlot.comboShowBtn.gameObject.SetActive(false);
        nineSlot.dreamComboShowBtn.gameObject.SetActive(false);
        nineSlot.comboCloseBtn.gameObject.SetActive(false);
        
        stoneBoxRect.gameObject.SetActive(false);
        nineSlot.IntroAboutCombo(true, dreamCombo);
        var returnLayer = UILayerLoader.Get<ReturnLayer>();
        returnLayer?.gameObject.SetActive(false);
        mask.gameObject.SetActive(true);
        EffectsManager.GenerateEffect("super_combo_explosion", FightGlobalSetting.EffectPathDefine(), 
            camConnector.FocusingC.WholeT.position, camConnector.FocusingC.WholeT.rotation, camConnector.FocusingC.WholeT).Forget();
        camConnector.FocusingC.AnimationManger.AddSpeedBuff("dreamCombo", 1.2f);
        
        var list = dreamCombo ? nineSlot.GetDreamComboStones() : nineSlot.GetRandomComboStones();
        for (var i = 0; i < list.Count; i++)
        {
            var stone = list[i];
            camConnector.FocusingC._MyBehaviorRunner._SkillCancelFlag.Cancel_Flag = false;
            await RunSkillAndShowTransition_Combo(stone, i < list.Count - 1 ? list[i+1] : null);
            await UniTask.WaitUntil(() => camConnector.FocusingC._MyBehaviorRunner._SkillCancelFlag.Cancel_Flag);
        }
        
        camConnector.FocusingC.AnimationManger.RemoveSpeedBuff("dreamCombo");
        
        mask.gameObject.SetActive(false);
        returnLayer?.gameObject.SetActive(true);
        
        nineSlot.comboShowBtn.gameObject.SetActive(true);
        nineSlot.dreamComboShowBtn.gameObject.SetActive(true);
        nineSlot.comboCloseBtn.gameObject.SetActive(PlayerAccountInfo.Me.tutorialProgress == "Finished");
    }
    
    void CloseComboShow()
    {
        nineSlot.IntroAboutCombo(false);
        stoneBoxRect.gameObject.SetActive(true);
        nineSlot.comboCloseBtn.gameObject.SetActive(false);
        stonesBox._tabEffects.TurnShowingTagEffects(true);
    }
    
    async UniTask RunSkillAndShowTransition_Combo(SKStoneItem stone, SKStoneItem endStone)
    {
        if (endStone != null)
        {
            var slot = nineSlot.GetSlotByCell(stone.GetCell());
            var endSlot = nineSlot.GetSlotByCell(endStone.GetCell());
            TransitionEffect(slot, endSlot);
        }
        //nineSlot.ShowTransition(slot, TransitionEffect);
        stonesBox._tabEffects.SkillButtonExplosion(stone._SkillConfig.SP_LEVEL, 
            PosCal.GetWorldPos(PreScene.target.postProcessCamera, stone.GetComponent<RectTransform>(), 3), 
            stonesBox._tabEffects.transform);
        await camConnector.SkillShowRunWithPrepare(stone._SkillConfig.REAL_NAME, false);
    }
    
    async UniTask RunSkillAndShowTransition(SKStoneItem stone)
    {
        var slot = nineSlot.GetSlotByCell(stone.GetCell());
        nineSlot.ShowTransition(slot, TransitionEffect);
        // stonesBox._tabEffects.SkillButtonExplosion(stone._SkillConfig.SP_LEVEL, 
        //     PosCal.GetWorldPos(PreScene.target.postProcessCamera, stone.GetComponent<RectTransform>(), 3), 
        //     stonesBox._tabEffects.transform);
        await camConnector.SkillShowRunWithPrepare(stone._SkillConfig.REAL_NAME, true);
    }
    
    async void TransitionEffect(SkillStoneSlot start, SkillStoneSlot end)
    {
        var startPos = PosCal.GetWorldPos(PreScene.target.postProcessCamera,
            start._cell.GetComponent<RectTransform>(), 3);
        var endPos = PosCal.GetWorldPos(PreScene.target.postProcessCamera,
            end._cell.GetComponent<RectTransform>(), 3);
                    
        var transitionEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>("gachastar0");
        _transitionEffects.Add(transitionEffect.gameObject);
        if (this == null)
        {
            Destroy(transitionEffect.gameObject);
            return;
        }
        transitionEffect.transform.position = startPos;
        var tween = transitionEffect.transform.DOMove(endPos, 1).OnComplete(
            async () =>
            {
                transitionEffect.Clear(true);
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                if (transitionEffect != null)
                {
                    Destroy(transitionEffect.gameObject);
                }
            }
        );
        _tweens.Add(tween);
    }

    public void OpenTip()
    {
        if (PlayerAccountInfo.Me.tutorialProgress == "Finished")
            UILayerLoader.Load<SkillEditTipLayer>();
    }
    
    public async UniTask Setup(Action<SkillEditLayer> toDo = null)
    {
        Initialized = false;
        //stonesBox.SetBoxHeight( 90 + 200 + 100);//90是那个距离条filter的高度，200是skillStoneDetail的高度，120是主观的额外空间
        SetGridGroupSize(stonesBox.Grid,0);
        stonesBox.GenerateCells(9);
        gameObject.SetActive(false);
        nineSlot.PrintSkillInfo = skillStoneDetail.RefreshInfo;
        nineSlot.StartUp(
            x=>
            {
                RunSkillAndShowTransition(x).Forget();
            }
        );
        stonesBox.AddFeatureToCells(StoneCellFeature);
        stonesBox.IniExTabs();
        
        var cts = new CancellationTokenSource();
        ReturnLayer.AddUniTaskCancel(cts);

        UnitConfig unitConfig = null;
        if (PreScene.target.Focusing != null)
        {
            unitConfig = Units.GetUnitConfig(PreScene.target.Focusing.r_id);
        }
        
        await stonesBox._tabEffects.SwitchElement(unitConfig != null? unitConfig.element : Element.lightMagic, cts.Token);
        await stonesBox.IniExTabsEffects(PreScene.target.postProcessCamera);
        stonesBox.FilterFeatureRefresh(true);
        skillStoneDetail.Clear();
        SkillEditButtonFeature(PreScene.target.Focusing);
        toDo?.Invoke(this);
        gameObject.SetActive(true);
        
        //ResizeCameraConnectorRefLeft(camConnector.GetComponent<RectTransform>(), cameraConnectorRightSpace, cameraConnectorVerticalSpace);
        //ResizeCameraConnectorRefTopAndSideWidth(camConnector.GetComponent<RectTransform>(), PosCal.VTopSafeAreaHeight,1100);
        var camRect = camConnector.GetComponent<RectTransform>();
        ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
        
        
        nineSlot.comboShowBtn.SetListener(()=> ShowCombo(false).Forget());
        nineSlot.dreamComboShowBtn.SetListener(()=> ShowCombo(true).Forget());
        nineSlot.comboCloseBtn.SetListener(CloseComboShow);
        
        Initialized = true;
    }
    
    public override void OnDestroy()
    {
        foreach (var tween in _tweens)
        {
            tween.Kill();
        }

        foreach (var effect in _transitionEffects)
        {
            if (effect != null)
                Destroy(effect);
        }
        _tweens.Clear();
        stonesBox._tabEffects.CloseShowingTagEffects();
    }

    void SkillEditButtonFeature(UnitInfo _unitInfo)
    {
        if (_unitInfo == null || _unitInfo.r_id == null)
        {
            Debug.Log("到达了没道理到达的地方");
            return;
        }
        nineSlot.ReadANineAndTwo(_unitInfo.id);
        var unitInfo = Units.GetUnitConfig(_unitInfo.r_id);
        stonesBox.FocusingType = unitInfo.TYPE;
        stonesBox.RestFilter();
        stonesBox.FilterFeatureRefresh(false);
        void SkillEditConfirm()
        {
            nineSlot.UpdateStonesBaseOnSlots(_unitInfo);
        }
        void SkillSetUpdate()
        {
            var valid = nineSlot.CheckEditBasedOnCurrent(PlayerAccountInfo.Me.tutorialProgress != "Finished");
            if (valid != SkillSet.SkillEditError.Perfect)
            {
                if (PlayerAccountInfo.Me.tutorialProgress != "Finished")
                {
                    switch (valid)
                    {
                        case SkillSet.SkillEditError.NoAtLeastTwoEx:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("AtLeastTwoEx"));
                            break;
                        case SkillSet.SkillEditError.UnBalanced:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("UnBalanced"));
                            break;
                        case SkillSet.SkillEditError.RepeatedSkill:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("CantEquipSameSkill"));
                            break;
                        case SkillSet.SkillEditError.NoNormalStart:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("AColumnNeedNormal"));
                            break;
                        case SkillSet.SkillEditError.NotFull:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("PlsFillAll"));
                            break;
                        default:
                            PopupLayer.ArrangeWarnWindow(Translate.Get("PlsFillAll"));
                            break;
                    }
                    return;
                }
                // 比如想给角色卸载全部技能的时候，虽然全部卸载后不能再战斗但是需要更新。
                PopupLayer.ArrangeConfirmWindow(SkillEditConfirm, Translate.Get("NotLegalButStillUpdate"));
            }
            else
            {
                SkillEditConfirm();
            }
        }
        
        nineSlot.ConfirmSkillChangeButton.onClick.AddListener(SkillSetUpdate);
        nineSlot.ResetButton.onClick.AddListener(nineSlot.ResetNineSlot);
        nineSlot.removeAllBtn.onClick.AddListener(nineSlot.ClearSkillEquip);
        nineSlot.randomBtn.onClick.AddListener(FinishRemains);
    }
    
    void ForceClearAll()
    {
        foreach (var slot in nineSlot.AllSlot)
        {
            slot._cell.RemoveToTemp();
        }
    }
    
    void StoneCellFeature(StoneCell cell)
    {
        void ButtonFeature()
        {
            var stone = cell.GetItem();
            if (stone != null && stone._SkillConfig != null)
            {
                skillStoneDetail.RefreshInfo(stone.instanceId);
            }else{
                skillStoneDetail.Clear();
            }
            StoneCell.SelectedRender(cell, SkillStonesBox.Selected);
        }
        
        void DoubleClick()
        {
            if (nineSlot.GetFocusingStoneSlot() != null)
            {
                StoneCell.Install(cell, nineSlot.GetFocusingStoneSlot()._cell);
            }
        }
        
        // 前往技能石升级画面
        void PressGoToLevelUpPage()
        {
            var stone = cell.GetItem();
            if (stone != null && stone._SkillConfig != null)
            {
                if (FightGlobalSetting.SkillStoneHasExp)
                    PreScene.target.trySwitchToStep(MainSceneStep.SkillStoneList, stone.instanceId, true);
            }
        }
        
        cell.btn.SetListener(ButtonFeature);
        cell.btn.ActivateHold = true;
        cell.btn.ActivateDoubleClick = true;
        
        cell.btn.onHold.AddListener(PressGoToLevelUpPage);
        cell.btn.onDoubleClick.AddListener(DoubleClick);
        cell.SetOnDropAction(StoneCell.Install);
    }
    
    public void SkillEditConfirmAnimation()
    {
        var personalEffectsPath = FightGlobalSetting.EffectPathDefine();
        EffectsManager.GenerateEffect("skillEditConfirmEffect", personalEffectsPath, camConnector.FocusingC.WholeT.position, Quaternion.identity, null).Forget();
    }
    
    public void OpenTutorial1()
    {
        clickNextTutorial1.Open();
    }
    
    public void OpenTutorial2()
    {
        clickNextTutorial2.Open();
    }
}

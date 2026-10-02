using DummyLayerSystem;
using mainMenu;
using System;

public class SkillEditTry : TutorialProcess
{
    private ReturnLayer _returnLayer;
    private SkillEditLayer _skillEditLayer;
    private LowerMainBar _lowerMainBar;
    private UpperInfoBar _upperInfoBar;
    private bool _skillEditFinished = false;
    private readonly string _tutorialFlag;
    private bool _active;
    private bool _configured;
    // Only the small progress marker is isolated for offline tutorial validation.
    private Action<string> _saveProgress = SaveProgress;

    public SkillEditTry(string tutorialFlag)
    {
        this._tutorialFlag = tutorialFlag;
    }
    
    public override void ProcessEnter()
    {
        _active = true;
        if (_tutorialFlag == "openInstruction1")
        {
            string focusInstanceID = PreScene.target.GetFocusInstanceID();
            PreScene.target.SetFocusingUnit(focusInstanceID);
            LowerMainBar.Open();
            MainMenuNote.GoingTo = MainSceneStep.UnitSkillEdit;
            PreScene.target.trySwitchToStep(MainMenuNote.GoingTo, false);
        }
    }
    
    public override bool CanEnterOtherProcess()
    {
        return _skillEditFinished;
    }
    
    public override void LocalUpdate()
    {
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
            if (_returnLayer != null)
            {
                _returnLayer.gameObject.SetActive(false);
            }
        }
        
        if (_lowerMainBar == null)
        {
            _lowerMainBar = UILayerLoader.Get<LowerMainBar>();
            if (_lowerMainBar != null)
            {
                _lowerMainBar.PlsClickBtn(MainSceneStep.None);
            }
        }
        
        if (_upperInfoBar == null)
        {
            _upperInfoBar = UILayerLoader.Get<UpperInfoBar>();
            if (_upperInfoBar != null)
            {
                _upperInfoBar.SetInteractive(false);
            }
        }
        
        if (!_skillEditFinished && _configured)
        {
            if (_skillEditLayer != null)
            {
                var validate = _skillEditLayer.nineSlot.ValidateWarn();
                _skillEditLayer.nineSlot.confirmBtnIndicator.SetActive(validate == SkillSet.SkillEditError.Perfect);
            }
        }
        
        if (_skillEditLayer == null)
        {
            _skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
        }
        else
        {
            if (_skillEditLayer.Initialized && !_configured)
            {
                string nextTutorialProgress = null;
                if (this._tutorialFlag == "openInstruction1")
                {
                    //_skillEditLayer.OpenTutorial1();
                    nextTutorialProgress = "SkillEditFinished";
                }
                
                if (this._tutorialFlag == "openInstruction2")
                {
                    //_skillEditLayer.OpenTutorial2();
                    nextTutorialProgress = "SkillEditFinished2";
                }
                
                _skillEditLayer.nineSlot.SetExtraSkillEditSuccess(
                    () =>
                    {
                        if (!_active || _skillEditFinished) return;
                        // Equipment has already been saved by the production skill
                        // update. A failed marker write must not strand that account.
                        PlayFabReadClient.RememberPendingTutorialProgress(nextTutorialProgress);
                        PlayerAccountInfo.Me.tutorialProgress = nextTutorialProgress;
                        PlayFabReadClient.DontShowFrontFight = "True";
                        _skillEditFinished = true;
                        _skillEditLayer.nineSlot.confirmBtnIndicator.SetActive(false);
                        _skillEditLayer.ClearTutorialGuidance();
                        _saveProgress(nextTutorialProgress);
                    }
                );
                _skillEditLayer.nineSlot.SetExtraOnNineSlotChanged(_skillEditLayer.ExtraTipForSpStoneEquip);
                _skillEditLayer.ExtraTipForSpStoneEquip();
                _configured = true;
                _skillEditLayer.stonesBox.ScrollRect.onValueChanged.AddListener(RefreshScrolledGuidance);
            }
        }
    }

    internal static void SaveProgress(string progress)
    {
        PlayFabReadClient.SaveTutorialProgressInBackground(progress);
    }

    void RefreshScrolledGuidance(UnityEngine.Vector2 position)
    {
        if (_active && _configured && !_skillEditFinished && _skillEditLayer != null)
            _skillEditLayer.ExtraTipForSpStoneEquip();
    }

    public override void ProcessEnd()
    {
        _active = false;
        if (_skillEditLayer != null)
        {
            _skillEditLayer.nineSlot.SetExtraSkillEditSuccess(null);
            _skillEditLayer.nineSlot.SetExtraOnNineSlotChanged(null);
            _skillEditLayer.stonesBox.ScrollRect.onValueChanged.RemoveListener(RefreshScrolledGuidance);
            _skillEditLayer.ClearTutorialGuidance();
        }
        _lowerMainBar?.CloseIndicators();
    }
}

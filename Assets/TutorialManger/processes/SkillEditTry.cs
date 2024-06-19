using System.Collections.Generic;
using DummyLayerSystem;
using mainMenu;
using PlayFab.ClientModels;

public class SkillEditTry : TutorialProcess
{
    private ReturnLayer _returnLayer;
    private SkillEditLayer _skillEditLayer;
    private LowerMainBar _lowerMainBar;
    private UpperInfoBar _upperInfoBar;
    private bool _skillEditFinished = false;
    private readonly string _tutorialFlag;

    public SkillEditTry(string tutorialFlag)
    {
        this._tutorialFlag = tutorialFlag;
    }
    
    public override void ProcessEnter()
    {
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
                _upperInfoBar.gameObject.SetActive(false);
            }
        }
        
        if (!_skillEditFinished)
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
            if (_skillEditLayer.Initialized)
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
                        PlayFabReadClient.UpdateUserData(
                            new UpdateUserDataRequest()
                            {
                                Data = new Dictionary<string, string>()
                                {
                                    { "TutorialProgress", nextTutorialProgress }
                                }
                            },
                            () =>
                            {
                                PlayerAccountInfo.Me.tutorialProgress = nextTutorialProgress;
                                PlayFabReadClient.DontShowFrontFight = "true";
                                _skillEditFinished = true;
                                _skillEditLayer.nineSlot.confirmBtnIndicator.SetActive(false);
                                _skillEditLayer.ExtraTipForSpStoneEquip();
                            }
                        );
                    }
                );
                _skillEditLayer.nineSlot.SetExtraOnNineSlotChanged(_skillEditLayer.ExtraTipForSpStoneEquip);
                _skillEditLayer.ExtraTipForSpStoneEquip();
                _skillEditLayer.Initialized = false;
            }
        }
    }
}

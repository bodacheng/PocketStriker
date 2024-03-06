using System.Collections.Generic;
using DummyLayerSystem;
using PlayFab.ClientModels;

public class SkillEditTry : TutorialProcess
{
    private ReturnLayer _returnLayer;
    private SkillEditLayer _skillEditLayer;
    private bool _skillEditFinished = false;
    private readonly string _tutorialFlag;

    public SkillEditTry(string tutorialFlag)
    {
        this._tutorialFlag = tutorialFlag;
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
        }
        if (_returnLayer != null)
        {
            _returnLayer.gameObject.SetActive(false);
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
                    _skillEditLayer.OpenTutorial1();
                    nextTutorialProgress = "SkillEditFinished";
                }
                
                if (this._tutorialFlag == "openInstruction2")
                {
                    _skillEditLayer.OpenTutorial2();
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
                            }
                        );
                    }
                );
                _skillEditLayer.Initialized = false;
            }
        }
    }
}

using DummyLayerSystem;
using mainMenu;

public class TryGotcha : TutorialProcess
{
    private GotchaLayer gotchaLayer;
    private GotchaResultLayer gotchaResultLayer;
    private LowerMainBar _lowerMainBar;
    private ReturnLayer returnLayer;
    private GotchaFront _gotchaFront;
    
    public override void LocalUpdate()
    {
        if (gotchaLayer == null)
        {
            gotchaLayer = UILayerLoader.Get<GotchaLayer>();
            if (gotchaLayer != null)
            {
                _gotchaFront = (GotchaFront)ProcessesRunner.Main.GetProcess(MainSceneStep.GotchaFront);
                _gotchaFront.SetExtraSuccessAction(
                    (x) =>
                    {
                        PlayFabReadClient.RememberPendingTutorialProgress("GotchaFinished");
                        PlayerAccountInfo.Me.tutorialProgress = "GotchaFinished";
                        SkillEditTry.SaveProgress("GotchaFinished");
                        x.Invoke();
                    }
                );
            }
        }
        
        if (returnLayer == null)
            returnLayer = UILayerLoader.Get<ReturnLayer>();
        if (returnLayer != null)
        {
            returnLayer.gameObject.SetActive(ProcessesRunner.Main.currentProcess.Step == MainSceneStep.DropTableInfo);
        }
        
        if (_lowerMainBar == null)
        {
            _lowerMainBar = UILayerLoader.Get<LowerMainBar>();
            if (_lowerMainBar != null)
            {
                _lowerMainBar.PlsClickBtn(MainSceneStep.None);
            }
        }
        
        if (gotchaResultLayer == null)
        {
            gotchaResultLayer = UILayerLoader.Get<GotchaResultLayer>();
        }

    }

    public override bool CanEnterOtherProcess()
    {
        return PlayerAccountInfo.Me.tutorialProgress == "GotchaFinished" && gotchaResultLayer != null && gotchaResultLayer.ShowFinished;
    }
    
    public override void ProcessEnd()
    {
        _gotchaFront?.SetExtraSuccessAction(null);
    }
}

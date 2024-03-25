using System.Collections.Generic;
using DummyLayerSystem;
using mainMenu;
using PlayFab.ClientModels;
using UnityEngine;

public class TryGotcha : TutorialProcess
{
    private GotchaLayer gotchaLayer;
    private GotchaResultLayer gotchaResultLayer;
    private LowerMainBar _lowerMainBar;
    private ReturnLayer returnLayer;
    
    public override void LocalUpdate()
    {
        if (gotchaLayer == null)
        {
            gotchaLayer = UILayerLoader.Get<GotchaLayer>();
            if (gotchaLayer != null)
            {
                var gotchaFront  = (GotchaFront)ProcessesRunner.Main.GetProcess(MainSceneStep.GotchaFront);
                gotchaFront.SetExtraSuccessAction(
                    () =>
                    {
                        PlayFabReadClient.UpdateUserData(
                            new UpdateUserDataRequest()
                            {
                                Data = new Dictionary<string, string>()
                                {
                                    { "TutorialProgress", "GotchaFinished" }
                                }
                            },
                            () =>
                            {
                                PlayerAccountInfo.Me.tutorialProgress = "GotchaFinished";
                            }
                        );
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
    }
}

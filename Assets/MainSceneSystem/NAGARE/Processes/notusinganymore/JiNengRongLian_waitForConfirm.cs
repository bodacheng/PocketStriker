//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using mainMenu;

//public class JiNengRongLian_waitForConfirm : MainSceneProcess
//{
//    RectTransform T;
//    public IEnumerator enterProcess()
//    {
//        if (this._preparingScene._RongLianGamen.canRongLianNow())
//        {
//            this.T.gameObject.SetActive(true);
//            this._preparingScene._RongLianGamen.trySwitchToWaitForConfirm(myModelPool.Instance.ModelDicBasedOnPlayerLocalID);
//        }
//        yield break;
//    }
    
//    public JiNengRongLian_waitForConfirm(preparingScene _preparingScene,RectTransform T)
//    {
//        this.thisProcessStep = MainSceneStep.JiNengRongLian_waitForConfirm;
//        this._preparingScene = _preparingScene;
//        this.T = T;
//    }

//    public override bool canEnterOtherProcess()
//    {
//        return true;
//    }
    
//    public override void ProcessEnter()
//    {
//        this._preparingScene.triggerMainProcess(enterProcess());
//    }
    
//    public override void ProcessEnd()
//    {
//        this.T.gameObject.SetActive(false);
//    }

//    public override void localUpdate()
//    {
//        this._preparingScene.showModelPositionAdjusting();
//    }
//}

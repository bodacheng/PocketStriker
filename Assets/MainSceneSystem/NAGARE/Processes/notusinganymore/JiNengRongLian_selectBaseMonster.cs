//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using mainMenu;

//public class JiNengRongLian_selectBaseMonster : MainSceneProcess
//{
//    RectTransform T;
//    public IEnumerator enterProcess()
//    {
//        foreach (KeyValuePair<int, GameObject> _pair in myModelPool.Instance.ModelDicBasedOnPlayerLocalID)
//        {
//            _pair.Value.SetActive(false);
//        }
//        this.T.gameObject.SetActive(true);
//        this._preparingScene._RongLianGamen.GUIRefresh();
//        this._preparingScene._MonsterBox.MonsterBoxWholeT.gameObject.SetActive(true);
//        yield return (this._preparingScene._MonsterBox.myMonsterBox());
//    }
    
//    public JiNengRongLian_selectBaseMonster(preparingScene _preparingScene,RectTransform T)
//    {
//        this.thisProcessStep = MainSceneStep.JiNengRongLian_selectBaseMonster;
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

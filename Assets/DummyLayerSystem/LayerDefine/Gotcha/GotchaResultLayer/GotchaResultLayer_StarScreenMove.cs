using UnityEngine;
using System.Collections.Generic;
using dataAccess;
using DG.Tweening;
using mainMenu;

public partial class GotchaResultLayer : UILayer
{
    #region 屏幕星星飞入位置
    [SerializeField] private float starScreenMoveDuration = 2;
    [SerializeField] RectTransform starWaitPos1, starWaitPos2, starWaitPos3, starWaitPos4, starWaitPos5, starWaitPos6, starWaitPos7, starWaitPos8, starWaitPos9;
    readonly List<RectTransform> waitPos = new List<RectTransform>();
    #endregion
    
    readonly List<Vector3> slotScreenPos = new List<Vector3>();
    
    void SetWaitPos()
    {
        waitPos.Clear();
        waitPos.Add(starWaitPos1);
        waitPos.Add(starWaitPos2);
        waitPos.Add(starWaitPos3);
        waitPos.Add(starWaitPos4);
        waitPos.Add(starWaitPos5);
        waitPos.Add(starWaitPos6);
        waitPos.Add(starWaitPos7);
        waitPos.Add(starWaitPos8);
        waitPos.Add(starWaitPos9);
    }
    
    void StarSortAnim(List<StoneOfPlayerInfo> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            StarScreenMoveAnim(results[i], PosCal.GetWorldPos(StarsFall.target.ECamera, waitPos[i], 30f), slotScreenPos[i]);
        }
    }
    
    /// <summary>
    /// 一颗星星从屏幕外移动向格子内的动画
    /// </summary>
    /// <param name="info"></param>
    /// <param name="waitPos"></param>
    /// <param name="endPos"></param>
    /// <returns></returns>
    void StarScreenMoveAnim(StoneOfPlayerInfo info, Vector3 waitPos, Vector3 endPos)
    {
        var effectSet = _effectDic[info];
        var screenStar = effectSet.StoneFigure;
        screenStar.ParticleSystem.Play();
        screenStar.transform.position = waitPos;
        effectSet.RunSequence(
            DOTween.Sequence().Append(screenStar.transform.DOMove(endPos, starScreenMoveDuration).OnComplete(
            () =>
            {
                screenStar.ParticleSystem.Stop();
                effectSet.ScreenExplosionFigure.transform.position = endPos;
                effectSet.ScreenExplosionFigure.ParticleSystem.Play(true);
                effectSet.ScreenExplosionFigure.PlaySoundOnce();
            })
        ));
    }
    
    // 必须使用时候即时运行因为里面几个决定位置的运算要考虑当前相机位置等
    void PosDecide()
    {
        slotScreenPos.Clear();
        // 星星落入格子
        void Process(RectTransform t)
        {
            var pos = PosCal.GetWorldPos(PreScene.target.postProcessCamera, t, 5f);
            slotScreenPos.Add(pos);
        }
        
        Process(NineForShow.A1T.GetComponent<RectTransform>());
        Process(NineForShow.A2T.GetComponent<RectTransform>());
        Process(NineForShow.A3T.GetComponent<RectTransform>());
        Process(NineForShow.B1T.GetComponent<RectTransform>());
        Process(NineForShow.B2T.GetComponent<RectTransform>());
        Process(NineForShow.B3T.GetComponent<RectTransform>());
        Process(NineForShow.C1T.GetComponent<RectTransform>());
        Process(NineForShow.C2T.GetComponent<RectTransform>());
        Process(NineForShow.C3T.GetComponent<RectTransform>());
    }
}

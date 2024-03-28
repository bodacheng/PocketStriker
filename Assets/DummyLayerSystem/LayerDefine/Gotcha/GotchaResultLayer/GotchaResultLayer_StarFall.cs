using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using dataAccess;
using DG.Tweening;

public partial class GotchaResultLayer : UILayer
{
    // 整个星星下落动画
    IEnumerator StarFallAnim(List<StoneOfPlayerInfo> results)
    {
        if (results != null)
        {
            foreach (var info in results)
            {
                StarFall(info);
                starFallAnimOneProcess = StartCoroutine(WaitForOneStarFall());
                while(!_oneStarFallen)
                    yield return new WaitForSeconds(0.1f);
            }
        }
        _starFallen = true;
    }
    
    void StarFall(StoneOfPlayerInfo stone)
    {
        var starExplosionInTheSkyPos = StarsFall.target.GetRandomStarPos();
        var effectSet = _effectDic[stone];
        var star = effectSet.StoneFigure;
        star.ParticleSystem.Play();
        star.PlaySoundOnce();
        star.transform.position = StarsFall.target.GetRandomStarPos(true);
        
        var cameraTargetPos = StarsFall.target.GetRandomStarPosCameraLookPos(starExplosionInTheSkyPos);
        StarsFall.target.Camera.transform.DOMove(cameraTargetPos, 1);
        
        var sequence = DOTween.Sequence().Append(star.transform.DOMove(starExplosionInTheSkyPos, 1).OnComplete(() =>
        {
            var flash = effectSet.StoneFlashFigure;
            flash.transform.position = starExplosionInTheSkyPos;
            flash.ParticleSystem.Play();
            flash.PlaySoundOnce();
        })).Append(star.transform.DOMove(cameraTargetPos + StarsFall.target.Camera.transform.forward * 5f, 1f));
        effectSet.RunSequence(sequence);
    }
    
    // 一个星星下落动画
    IEnumerator WaitForOneStarFall()
    {
        _oneStarFallen = false;
        yield return new WaitForSecondsRealtime(2f);
        _oneStarFallen = true;
    }
    
    // 加速一个星星下落动画
    void SpeedOneGotchaAnim()
    {
        if (starFallAnimOneProcess != null)
        {
            StopCoroutine(starFallAnimOneProcess);
        }
        _oneStarFallen = true;
    }
    
    // 跳过整个星星下落动画
    void SkipStarFallAnim()
    {
        if (starFallAnimWholeProcess != null)
        {
            StopCoroutine(starFallAnimWholeProcess);
        }
        SpeedOnce.gameObject.SetActive(false);
        _starFallen = true;
    }
}

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CountDownLayer : UILayer
{
    [Header("CountDownText")]
    [SerializeField] Text CountDown;
    [SerializeField] float startTimestamp = 3f;
    [SerializeField] Image readyObject;
    [SerializeField] Image goObject;
    [SerializeField] private float textAnimationDuration = 1;
    private bool gone = false;
    private float titleAnimFactor = 0;
    
    public async UniTask BeforeFightCountDown()
    {
        readyObject.gameObject.SetActive(true);
        var tween = DOTween.To(() => titleAnimFactor, (x) => titleAnimFactor = x, 2, textAnimationDuration).OnUpdate(() =>
        {
            readyObject.material.SetFloat("_Animation_Factor", titleAnimFactor);
        });
        while (startTimestamp > 0)
        {
            startTimestamp -= Time.deltaTime;
            CountDown.text = "" + (1 + (int)(startTimestamp));
            if (startTimestamp < 1.5 && !gone)
            {
                tween.Kill();
                readyObject.gameObject.SetActive(false);
                goObject.gameObject.SetActive(true);
                titleAnimFactor = 0;
                tween = DOTween.To(() => titleAnimFactor, (x) => titleAnimFactor = x, 2, textAnimationDuration).OnUpdate(() =>
                {
                    goObject.material.SetFloat("_Animation_Factor", titleAnimFactor);
                });
                gone = true;
            }
            await UniTask.DelayFrame(0);
        }
    }
}

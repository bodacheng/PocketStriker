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
        var remaining = Mathf.Max(0f, startTimestamp);
        gone = false;
        titleAnimFactor = 0f;
        CountDown.text = string.Empty;
        readyObject.gameObject.SetActive(true);
        goObject.gameObject.SetActive(false);

        var tween = DOTween.To(() => titleAnimFactor, (x) => titleAnimFactor = x, 2, textAnimationDuration).OnUpdate(() =>
        {
            readyObject.material.SetFloat("_Animation_Factor", titleAnimFactor);
        });

        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            CountDown.text = "" + Mathf.Max(1, 1 + (int)remaining);
            if (remaining < 1.5f && !gone)
            {
                tween.Kill();
                readyObject.gameObject.SetActive(false);
                goObject.gameObject.SetActive(true);
                titleAnimFactor = 0f;
                tween = DOTween.To(() => titleAnimFactor, (x) => titleAnimFactor = x, 2, textAnimationDuration).OnUpdate(() =>
                {
                    goObject.material.SetFloat("_Animation_Factor", titleAnimFactor);
                });
                gone = true;
            }
            await UniTask.Yield();
        }

        tween.Kill();
    }
}

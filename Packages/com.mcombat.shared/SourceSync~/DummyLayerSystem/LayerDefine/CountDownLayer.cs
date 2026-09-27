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
    private Tween _countDownTween;
    private bool _isDestroyed;
    
    public async UniTask BeforeFightCountDown()
    {
        if (CountDown == null || readyObject == null || goObject == null)
        {
            return;
        }

        _isDestroyed = false;
        var remaining = Mathf.Max(0f, startTimestamp);
        gone = false;
        titleAnimFactor = 0f;
        CountDown.text = string.Empty;
        readyObject.gameObject.SetActive(true);
        goObject.gameObject.SetActive(false);

        _countDownTween = PlayTitleTween(readyObject);

        while (remaining > 0f && !_isDestroyed)
        {
            remaining -= Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            CountDown.text = "" + Mathf.Max(1, 1 + (int)remaining);
            if (remaining < 1.5f && !gone)
            {
                _countDownTween?.Kill();
                _countDownTween = null;
                readyObject.gameObject.SetActive(false);
                goObject.gameObject.SetActive(true);
                titleAnimFactor = 0f;
                _countDownTween = PlayTitleTween(goObject);
                gone = true;
            }
            await UniTask.Yield();
        }

        _countDownTween?.Kill();
        _countDownTween = null;
    }

    Tween PlayTitleTween(Image target)
    {
        if (target == null)
        {
            return null;
        }

        return DOTween.To(() => titleAnimFactor, x => titleAnimFactor = x, 2, textAnimationDuration)
            .SetLink(target.gameObject)
            .OnUpdate(() =>
            {
                if (_isDestroyed || target == null)
                {
                    return;
                }

                target.material.SetFloat("_Animation_Factor", titleAnimFactor);
            });
    }

    public override void OnDestroy()
    {
        _isDestroyed = true;
        _countDownTween?.Kill();
        _countDownTween = null;
        base.OnDestroy();
    }
}

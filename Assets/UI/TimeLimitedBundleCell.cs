using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;

public class TimeLimitedBundleCell : MonoBehaviour
{
    [SerializeField] Text countDownText;
    [SerializeField] Text msg;
    [SerializeField] Text dmAmount;
    
    private IDisposable _disposeSeasonCountDown;
    
    public void ShowTimeLimitedBundle(TimeLimitedBuyData data)
    {
        _disposeSeasonCountDown?.Dispose();
        _disposeSeasonCountDown = null;

        DateTime endTime = default;
        var on = data != null &&
                 TimeLimitedSaleWindow.TryGetActiveEndUtc(data.startTime, data.endTime, DateTime.UtcNow, out endTime) &&
                 ShopTop.HasTimeLimitSale(data);
        if (!on)
        {
            gameObject.SetActive(false);
            return;
        }
        
        gameObject.SetActive(true);
        msg.text = data.message;
        dmAmount.text = data.dmAmount.ToString();
        
        _disposeSeasonCountDown = 
            Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(
                (_) =>
                {
                    var timeRemaining = endTime - DateTime.UtcNow;
                    countDownText.text = timeRemaining.ToString(@"dd\:hh\:mm\:ss");
                    if (timeRemaining.TotalSeconds <= 0)
                    {
                        gameObject.SetActive(false);
                    }
                }).AddTo(gameObject);
    }

    void OnDisable()
    {
        _disposeSeasonCountDown?.Dispose();
        _disposeSeasonCountDown = null;
    }
}

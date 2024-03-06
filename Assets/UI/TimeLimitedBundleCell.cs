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
        var on = ShopTop.HasTimeLimitSale(data);
        if (!on)
        {
            gameObject.SetActive(false);
            return;
        }
        
        gameObject.SetActive(true);
        msg.text = data.message;
        dmAmount.text = data.dmAmount.ToString();
        
        DateTime endTime = DateTime.Parse(data.endTime);
        TimeSpan timeRemaining = endTime - DateTime.UtcNow;    
        _disposeSeasonCountDown = 
            Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(
                (_) =>
                {
                    timeRemaining = timeRemaining.Subtract(TimeSpan.FromSeconds(1));
                    countDownText.text = timeRemaining.ToString(@"dd\:hh\:mm\:ss");
                    if (timeRemaining.TotalSeconds <= 0)
                    {
                        gameObject.SetActive(false);
                        _disposeSeasonCountDown.Dispose();
                    }
                }).AddTo(gameObject);
    }
}

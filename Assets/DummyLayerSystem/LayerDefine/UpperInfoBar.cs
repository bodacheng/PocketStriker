using UnityEngine.UI;
using UnityEngine;
using System;
using UniRx;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

public class UpperInfoBar : UILayer
{
    [SerializeField] BOButton settingBtn;
    [SerializeField] BOButton mailBtn;
    [SerializeField] GameObject unReadFlag;
    [SerializeField] Text titleDisplayName;
    [SerializeField] Text accountDiamondCoin;
    [SerializeField] BOButton diamondPlus;
    [SerializeField] GameObject hasTimeLimitSaleFlag;
    [SerializeField] Text accountIntelliCoin;
    [SerializeField] GameObject vipFlg;
    [SerializeField] float currencyTextChangeDuration = 2f;

    private string gold;
    private string GoldText
    {
        set
        {
            if (gold != value)
            {
                _tweenTextScaleManager.AddNew(accountIntelliCoin.transform, Vector3.one * 1.2f, Vector3.one, RewardTextChangeHalfDuration);
            }
            gold = value;
            accountIntelliCoin.text = gold;
        }
        get => gold;
    }
    
    private string diamond;
    private string DiamondText
    {
        set
        {
            if (diamond != value)
            {
                _tweenTextScaleManager.AddNew(accountDiamondCoin.transform, Vector3.one * 1.2f, Vector3.one, RewardTextChangeHalfDuration);
            }
            diamond = value;
            accountDiamondCoin.text = diamond;
        }
        get => diamond;
    }
    
    private readonly TweenTextScaleManager _tweenTextScaleManager = new TweenTextScaleManager();
    private const float RewardTextChangeHalfDuration = 0.05f;
    private TweenerCore<int, int, NoOptions> _gdTween;
    private TweenerCore<int, int, NoOptions> _dmTween;
    
    public void Interactable(bool on)
    {
        settingBtn.interactable = on;
        mailBtn.interactable = on;
        diamondPlus.interactable = on;
    }
    
    public void Setup(string titleDisplayName, Action openSetting, Action openMail, Action openDmShop, bool isVip)
    {
        this.titleDisplayName.text = titleDisplayName;
        DiamondText = Currencies.DiamondCount.Value.ToString();
        Currencies.DiamondCount.Subscribe(x =>
        {
            int.TryParse(DiamondText, out int currentValue);
            int targetValue = currentValue;
            _dmTween = DOTween.To(
                () => targetValue,
                setterValue => targetValue = setterValue,
                x,
                currencyTextChangeDuration
            ).OnUpdate(() =>
            {
                DiamondText = targetValue.ToString();
            });
        }).AddTo(this.gameObject);
        
        GoldText = Currencies.CoinCount.Value.ToString();
        Currencies.CoinCount.Subscribe(x =>
        {
            int.TryParse(GoldText, out int currentValue);
            int targetValue = currentValue;
            
            _gdTween = DOTween.To(
                () => targetValue,
                setterValue => targetValue = setterValue,
                x,
                currencyTextChangeDuration
            ).OnUpdate(() =>
            {
                GoldText = targetValue.ToString();
            });
        }).AddTo(this.gameObject);
        
        unReadFlag.SetActive(PlayFabReadClient.GetMailsData(true).Count > 0);
        if (openSetting != null)
        {
            settingBtn.onClick.AddListener(openSetting.Invoke);
            settingBtn.gameObject.SetActive(true);
        }
        else
        {
            settingBtn.gameObject.SetActive(false);
        }

        if (openMail != null)
        {
            mailBtn.onClick.AddListener(openMail.Invoke);
            mailBtn.gameObject.SetActive(true);
        }
        else
        {
            mailBtn.gameObject.SetActive(false);
        }

        if (openDmShop != null)
        {
            diamondPlus.onClick.AddListener(openDmShop.Invoke);
            diamondPlus.gameObject.SetActive(true);
        }
        else
        {
            diamondPlus.gameObject.SetActive(false);
        }

        IAPManager.Target.IsInitialized.Subscribe(x =>
        {
            diamondPlus.interactable = x;
        }).AddTo(this.gameObject);
        
        hasTimeLimitSaleFlag.SetActive(ShopTop.HasTimeLimitSale(PlayFabReadClient.TimeLimitedBuyData));
        
        vipFlg.SetActive(isVip);
    }

    public override void OnDestroy()
    {
        _tweenTextScaleManager.Clear();
        _gdTween?.Kill();
        _dmTween?.Kill();
        base.OnDestroy();
    }
}

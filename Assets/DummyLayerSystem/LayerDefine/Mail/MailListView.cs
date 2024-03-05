using UnityEngine.UI;
using UnityEngine;
using mainMenu;
using System;
using UniRx;

/// <summary>
/// 邮件ListView的开发主要有如下问题
/// 1. 邮件icon。这个取决于这个邮件送的礼物是什么
/// 2. 是否已经提取了邮件中的礼物的flag。这个简单做的话就是覆盖在icon上面的一个"Got"标志
/// 3. 邮件的基本文本显示
/// 4. 获取倒计时
///
/// 一个有点麻烦的地方在于，如果这个邮件箱的空间有限，那么邮件箱内部邮件有累积并超过了邮件箱的尺寸的话...
/// 我们想到一个问题是login bonus和邮件礼物真是两码事
/// </summary>

public class MailListView : MonoBehaviour
{
    [SerializeField] Image bg;
    [SerializeField] Image mailIcon;
    [SerializeField] Text title;
    [SerializeField] Text expiration;
    [SerializeField] GameObject unReadFlag;
    [SerializeField] BOButton claimBtn;
    [SerializeField] BOButton detailBtn;
    
    [SerializeField] Color unreadc = new Color(0.4f,0.4f,1, 1);
    [SerializeField] Color readc = new Color(0.4f,0.4f,1, 0.6f); 

    string _itemInstanceId;
    private Action<Image, string> _iconRefresh;
    private IDisposable disposeCountDown;
    
    public void Setup(Action<Image, string> iconRefresh)
    {
        this._iconRefresh = iconRefresh;
    }
    
    public void PassMailInfo(MailItemInstance mailData, Action sort)
    {
        _itemInstanceId = mailData.ItemInstanceId;
        title.text = mailData.DisplayName;
        _iconRefresh(mailIcon, mailData.ItemId);
        if (mailData.Expiration.HasValue)
        {
            disposeCountDown = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe((_) =>
            {
                var difference = mailData.Expiration.Value - DateTime.Now;
                difference = difference.Subtract(TimeSpan.FromSeconds(1));
                expiration.text = difference.ToString(@"dd\:hh\:mm\:ss");
            }).AddTo(gameObject);
        }
        else
        {
            expiration.gameObject.SetActive(false);
        }
        
        mailData.ReadObservable.Subscribe(AsRead).AddTo(this.gameObject);
        
        claimBtn.onClick.RemoveAllListeners();
        claimBtn.onClick.AddListener(
            () => PlayFabReadClient.ClaimPresent(
                mailData.ItemId,
                mailData.CatalogVersion,
                x =>
                {
                    PlayFabReadClient.SaveReadMailAsJson(x);
                    sort.Invoke();
                }
            )
        );
        
        //  暂不需要详细读取邮件功能
        detailBtn.onClick.RemoveAllListeners();
        detailBtn.onClick.AddListener(ReadMail);
        
        mailData.Set();
    }
    
    void AsRead(bool read)
    {
        claimBtn.gameObject.SetActive(!read);
        expiration.gameObject.SetActive(!read);
        claimBtn.gameObject.SetActive(!read);
        unReadFlag.SetActive(!read);
        bg.color = read ? readc : unreadc;
    }
    
    void ReadMail()
    {
        PreScene.target.trySwitchToStep(MainSceneStep.MailDetail, _itemInstanceId, true);
    }
}
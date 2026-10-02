using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using DummyLayerSystem;

public class TitleBgLayer : UILayer
{
    [SerializeField] Image targetImage;
    [SerializeField] Button touchScreenBtn;
    [SerializeField] BOButton skipBtn;
    [SerializeField] LanguageConverter languageConverter;
    [SerializeField] RectTransform content;
    [SerializeField] Scrollbar vScrollbar;
    [SerializeField] List<string> subtitleCodes;
    [SerializeField] float scrollDelayFromSeconds = 1;
    [SerializeField] float scrollDelayInMilliSecond = 0.001f;
    [SerializeField] float ScrollbarMinValue = 0;
    [SerializeField] float ScrollbarMaxValue = 1;
    float _milliSecondCounter = 0;
    IDisposable _disposable;
    bool loginArtwork;
    
    /// <summary>Static, aspect-preserving login art; the original scroll remains available for authored intros.</summary>
    public async UniTask SetupLogin()
    {
        var sprite = await AddressablesLogic.LoadT<Sprite>("LoginArt", gameObject);
        if (this == null) return;
        if (sprite == null) throw new InvalidOperationException("Required login artwork is missing: LoginArt");
        _disposable?.Dispose();
        loginArtwork = true;
        targetImage.sprite = sprite;
        targetImage.color = Color.white;
        targetImage.raycastTarget = false;
        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.enabled = false;
        vScrollbar.gameObject.SetActive(false);
        languageConverter.gameObject.SetActive(false);
        skipBtn.gameObject.SetActive(false);
        touchScreenBtn.gameObject.SetActive(false);
        FitLoginArtwork();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        FitLoginArtwork();
    }

    void FitLoginArtwork()
    {
        if (!loginArtwork || content == null || targetImage == null || targetImage.sprite == null) return;
        var viewport = content.parent as RectTransform;
        if (viewport == null || viewport.rect.width <= 0 || viewport.rect.height <= 0) return;
        var size = targetImage.sprite.rect.size;
        float scale = Mathf.Max(viewport.rect.width / size.x, viewport.rect.height / size.y);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
        content.sizeDelta = size * scale;
        content.anchoredPosition = Vector2.zero;
    }

    public async UniTask Setup(float scrollValue) // false: titleMode
    {
        var targetSprite = await AddressablesLogic.LoadT<Sprite>("TitleBg", this.gameObject);
        if (targetSprite == null)
            throw new InvalidOperationException("Required title background asset is missing: TitleBg");
        var parentRect = transform.GetComponent<RectTransform>();
        content.sizeDelta = new Vector2(parentRect.rect.width ,  targetSprite.rect.height * parentRect.rect.width / targetSprite.rect.width);
        content.anchoredPosition = Vector2.zero;
        targetImage.sprite = targetSprite;
        vScrollbar.value = scrollValue;
        targetImage.color = Color.white;
    }
    
    public void Rotate(bool storyMode, Action onClickProcess = null)
    {
        if (storyMode)
        {
            touchScreenBtn.onClick.AddListener(() =>
            {
                UILayerLoader.Remove<TitleBgLayer>();
                onClickProcess?.Invoke();
            });
            skipBtn.gameObject.SetActive(true);
            skipBtn.onClick.AddListener(() =>
            {
                UILayerLoader.Remove<TitleBgLayer>();
                onClickProcess?.Invoke();
            });
        }
        
        void ChangeSubtitle(int codeIndex)
        {
            if (subtitleCodes.Count > codeIndex)
                languageConverter.ChangeAtOnce(subtitleCodes[codeIndex]);
        }
        _disposable = Observable.Timer(TimeSpan.FromSeconds(storyMode? scrollDelayFromSeconds : 0), TimeSpan.FromMilliseconds(0.1)).Subscribe(
            (_) =>
            {
                _milliSecondCounter += scrollDelayInMilliSecond;
                vScrollbar.value = Mathf.Clamp(vScrollbar.value - scrollDelayInMilliSecond, ScrollbarMinValue, ScrollbarMaxValue);
                if (storyMode)
                {
                    languageConverter.gameObject.SetActive(storyMode);
                    var indexOfSubtitleCodes = (int)((1 - vScrollbar.value) / (1f / subtitleCodes.Count));
                    ChangeSubtitle(indexOfSubtitleCodes);
                    if (_milliSecondCounter >= 1.2)
                    {
                        languageConverter.gameObject.SetActive(false);
                        touchScreenBtn.gameObject.SetActive(true);
                        _disposable.Dispose();
                    }
                }
                else
                {
                    if (vScrollbar.value <= ScrollbarMinValue)
                        _disposable.Dispose();
                }
            }).AddTo(gameObject);
    }
}

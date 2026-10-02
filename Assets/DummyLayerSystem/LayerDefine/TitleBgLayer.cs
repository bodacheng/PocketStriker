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
    Image loginFooterShade;
    Texture2D loginFooterGradient;
    Sprite loginFooterSprite;
    
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
        // The authored sprite pivot defines which quiet art region survives
        // aspect fill. The multiverse poster keeps its pale title space at the
        // top; older center-pivot artwork retains its existing composition.
        var pivot = new Vector2(targetImage.sprite.pivot.x / size.x, targetImage.sprite.pivot.y / size.y);
        content.anchorMin = content.anchorMax = content.pivot = pivot;
        content.sizeDelta = size * scale;
        content.anchoredPosition = Vector2.zero;
        FitLoginFooterShade(viewport, pivot, size.y * scale);
    }

    void FitLoginFooterShade(RectTransform viewport, Vector2 pivot, float artworkHeight)
    {
        float croppedFraction = Mathf.Max(0, 1 - viewport.rect.height / artworkHeight);
        float strength = pivot.y > .5f ? Mathf.InverseLerp(.04f, .2f, croppedFraction) : 0;
        if (strength <= 0)
        {
            if (loginFooterShade != null) loginFooterShade.gameObject.SetActive(false);
            return;
        }
        if (loginFooterShade == null)
        {
            // Top-aligned tablet cropping removes the poster's dark footer.
            // A native UI gradient restores contrast for the existing white
            // start hint without moving controls or covering the central gem.
            loginFooterGradient = new Texture2D(1, 32, TextureFormat.RGBA32, false)
                {name = "Login footer gradient", hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear};
            for (int y = 0; y < loginFooterGradient.height; y++)
            {
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1, y / 31f));
                loginFooterGradient.SetPixel(0, y, new Color(.025f, .07f, .13f, .88f * fade));
            }
            loginFooterGradient.Apply(false, true);
            loginFooterSprite = Sprite.Create(loginFooterGradient, new Rect(0, 0, 1, 32), new Vector2(.5f, .5f));
            loginFooterSprite.name = "Login footer shade";
            loginFooterSprite.hideFlags = HideFlags.DontSave;
            var shadeObject = new GameObject("LoginFooterShade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadeObject.layer = viewport.gameObject.layer;
            shadeObject.transform.SetParent(viewport, false);
            loginFooterShade = shadeObject.GetComponent<Image>();
            loginFooterShade.sprite = loginFooterSprite;
            loginFooterShade.raycastTarget = false;
            var rect = loginFooterShade.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, .26f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        loginFooterShade.gameObject.SetActive(true);
        loginFooterShade.color = new Color(1, 1, 1, strength);
        loginFooterShade.transform.SetAsLastSibling();
    }

    public override void OnDestroy()
    {
        _disposable?.Dispose();
        if (loginFooterSprite != null) Destroy(loginFooterSprite);
        if (loginFooterGradient != null) Destroy(loginFooterGradient);
        base.OnDestroy();
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

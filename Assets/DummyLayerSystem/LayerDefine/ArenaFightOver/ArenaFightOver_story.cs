using UnityEngine.UI;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

public partial class ArenaFightOver : UILayer
{
    [SerializeField] private BOButton storyMaskBtn;
    [SerializeField] private Image storyBgImage;
    [SerializeField] private Text shortStory;
    [SerializeField] private Color storyBgFromColor;
    [SerializeField] private Color storyBgToColor;
    [SerializeField] private Color gbStoryBgToColor;
    [SerializeField] private float storyBgColorChangeDuration;
    [SerializeField] private AudioSource storyLayerAudio;
    
    private TweenerCore<Color, Color, ColorOptions> storyBgColorChangeTween;
    public bool LoadStory()
    {
        var code = FightLoad.Fight.ID;

        switch (FightLoad.Fight.EventType)
        {
            case FightEventType.Quest:
                shortStory.text = ShortStory.Get(code);
                break;
            case FightEventType.Gangbang:
                shortStory.text = GBShortStory.Get(code);
                break;
            default:
                break;
        }
        
        storyLayerAudio.volume = AppSetting.Value.BgmVolume;
        
        bool notNull = !string.IsNullOrEmpty(shortStory.text);
        storyBgImage.gameObject.SetActive(notNull);
        shortStory.gameObject.SetActive(notNull);
        if (notNull)
        {
            storyBgImage.color = storyBgFromColor;
            storyBgColorChangeTween = storyBgImage.DOColor(FightLoad.Fight.EventType == FightEventType.Quest ? storyBgToColor : gbStoryBgToColor
                , storyBgColorChangeDuration);
        }
        return notNull;
    }
}

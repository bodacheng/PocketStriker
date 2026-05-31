using System;
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
    private StoryInfo activeAIStory;
    private int aiStorySceneIndex;
    private int aiStoryLineIndex;
    private bool aiStoryPlaying;
    private Action storyFinishedCallback;

    public bool LoadStory()
    {
        if (TryLoadAIStory())
        {
            return true;
        }

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
            storyBgImage.sprite = null;
            storyBgImage.preserveAspect = false;
            storyBgImage.color = storyBgFromColor;
            storyBgColorChangeTween = storyBgImage.DOColor(FightLoad.Fight.EventType == FightEventType.Quest ? storyBgToColor : gbStoryBgToColor
                , storyBgColorChangeDuration);
        }
        return notNull;
    }

    private bool TryLoadAIStory()
    {
        var story = FightScene.FightScene.target?.AIStoryInfo;
        var firstSceneIndex = story?.FindNextVisualSceneIndex(-1) ?? -1;
        if (firstSceneIndex < 0)
        {
            if (story != null)
            {
                FightScene.FightScene.target?.AIServiceManager?.MarkAIStoryAsShown();
            }
            return false;
        }

        activeAIStory = story;
        aiStoryPlaying = true;
        aiStorySceneIndex = firstSceneIndex;
        aiStoryLineIndex = -1;

        storyLayerAudio.volume = AppSetting.Value.BgmVolume;
        storyBgColorChangeTween?.Kill();
        storyBgImage.gameObject.SetActive(true);
        shortStory.gameObject.SetActive(false);
        storyMaskBtn.SetListener(AdvanceAIStory);
        AIServiceManager.LogStoryContentForDebug(story);
        DisplayAIStoryScene();
        return true;
    }

    private void AdvanceAIStory()
    {
        if (!aiStoryPlaying || activeAIStory?.StoryScenes == null || activeAIStory.StoryScenes.Count == 0)
        {
            FinishAIStory();
            return;
        }

        var scene = activeAIStory.StoryScenes[aiStorySceneIndex];
        var lines = scene?.Lines;
        if (lines != null && aiStoryLineIndex < lines.Count - 1)
        {
            aiStoryLineIndex++;
            DisplayAIStoryLine();
            return;
        }

        var nextSceneIndex = activeAIStory.FindNextVisualSceneIndex(aiStorySceneIndex);
        if (nextSceneIndex >= 0)
        {
            aiStorySceneIndex = nextSceneIndex;
            DisplayAIStoryScene();
            return;
        }

        FinishAIStory();
    }

    private void DisplayAIStoryScene()
    {
        if (activeAIStory?.StoryScenes == null || aiStorySceneIndex >= activeAIStory.StoryScenes.Count)
        {
            FinishAIStory();
            return;
        }

        aiStoryLineIndex = -1;
        var scene = activeAIStory.StoryScenes[aiStorySceneIndex];
        if (scene?.Pic == null)
        {
            var nextSceneIndex = activeAIStory.FindNextVisualSceneIndex(aiStorySceneIndex);
            if (nextSceneIndex < 0)
            {
                FinishAIStory();
                return;
            }

            aiStorySceneIndex = nextSceneIndex;
            scene = activeAIStory.StoryScenes[aiStorySceneIndex];
        }

        storyBgImage.sprite = scene.Pic;
        storyBgImage.preserveAspect = true;
        storyBgImage.color = Color.white;
        shortStory.text = string.Empty;
        shortStory.gameObject.SetActive(false);
    }

    private void DisplayAIStoryLine()
    {
        var scene = activeAIStory?.StoryScenes?[aiStorySceneIndex];
        string displayLine = string.Empty;
        if (scene?.Lines != null && aiStoryLineIndex >= 0 && aiStoryLineIndex < scene.Lines.Count)
        {
            displayLine = scene.Lines[aiStoryLineIndex];
        }

        shortStory.text = displayLine;
        shortStory.gameObject.SetActive(!string.IsNullOrWhiteSpace(displayLine));
    }

    private void FinishAIStory()
    {
        aiStoryPlaying = false;
        activeAIStory = null;
        storyBgImage.gameObject.SetActive(false);
        shortStory.gameObject.SetActive(false);
        FightScene.FightScene.target?.AIServiceManager?.MarkAIStoryAsShown();
        var callback = storyFinishedCallback;
        storyFinishedCallback = null;
        callback?.Invoke();
    }
}

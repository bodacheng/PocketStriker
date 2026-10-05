using UnityEngine;
using UnityEngine.UI;

public partial class ArenaFightOver
{
    private GameObject aiStoryPresentation;
    private GameObject aiStoryCaptionPanel;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (aiStoryPlaying && aiStoryPresentation != null && aiStoryCaptionPanel != null
            && shortStory != null && !string.IsNullOrWhiteSpace(shortStory.text)) ResizeAIStoryCaption();
    }

    private void ResizeAIStoryCaption()
    {
        var root = (RectTransform)aiStoryPresentation.transform;
        var caption = shortStory.rectTransform;
        if (root.rect.height <= 0 || caption.rect.width <= 0) return;
        float maximumHeight = root.rect.height * .185f;
        var settings = shortStory.GetGenerationSettings(new Vector2(caption.rect.width, maximumHeight));
        settings.resizeTextForBestFit = false;
        settings.fontSize = shortStory.resizeTextMaxSize;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        float preferredHeight = new TextGenerator().GetPreferredHeight(shortStory.text, settings) / shortStory.pixelsPerUnit;
        float height = Mathf.Clamp(preferredHeight + 2, settings.fontSize * shortStory.lineSpacing, maximumHeight);
        caption.anchorMax = new Vector2(caption.anchorMax.x, caption.anchorMin.y + height / root.rect.height);
        var backdrop = (RectTransform)aiStoryCaptionPanel.transform;
        backdrop.anchorMax = new Vector2(backdrop.anchorMax.x, caption.anchorMax.y + .02f);
    }

    private void PrepareAIStoryPresentation()
    {
        if (aiStoryPresentation != null)
        {
            aiStoryPresentation.SetActive(true);
            return;
        }

        // The authored-story background has a sprite Animator. It overwrites a
        // generated picture every frame; AI artwork needs an ordinary UI Image.
        var font = shortStory.font;
        storyBgImage.gameObject.SetActive(false);
        shortStory.gameObject.SetActive(false);
        aiStoryPresentation = new GameObject("AI Story Presentation", typeof(RectTransform), typeof(Image), typeof(BOButton));
        var root = (RectTransform)aiStoryPresentation.transform;
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        var backdrop = aiStoryPresentation.GetComponent<Image>();
        backdrop.color = new Color32(20, 28, 38, 255);
        storyMaskBtn = aiStoryPresentation.GetComponent<BOButton>();
        storyMaskBtn.targetGraphic = backdrop;
        storyMaskBtn.transition = Selectable.Transition.None;
        storyMaskBtn.navigation = new Navigation { mode = Navigation.Mode.None };

        RectTransform Region(string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        Text Caption(string name, Vector2 min, Vector2 max, int size, Color color, TextAnchor alignment)
        {
            var text = Region(name, min, max).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color;
            text.alignment = alignment; text.lineSpacing = 1.15f;
            text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        // Let the portrait artwork use the whole game surface. Captions appear
        // over it as the player advances, so reading never shrinks the picture.
        storyBgImage = Region("Generated Illustration", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        storyBgImage.raycastTarget = false;
        storyBgImage.preserveAspect = true;
        var captionBackdrop = Region("Caption Backdrop", new Vector2(.025f, .085f), new Vector2(.975f, .31f)).gameObject.AddComponent<Image>();
        captionBackdrop.color = new Color32(12, 18, 26, 224);
        captionBackdrop.raycastTarget = false;
        aiStoryCaptionPanel = captionBackdrop.gameObject;
        aiStoryCaptionPanel.SetActive(false);
        shortStory = Caption("Story Caption", new Vector2(.055f, .105f), new Vector2(.945f, .29f), 44,
            new Color32(241, 238, 227, 255), TextAnchor.UpperLeft);
        shortStory.resizeTextForBestFit = true;
        shortStory.resizeTextMinSize = 28;
        shortStory.resizeTextMaxSize = 44;

        var language = AIStoryRuntimeContext.GetLanguage();
        bool chinese = language == SystemLanguage.Chinese || language == SystemLanguage.ChineseSimplified || language == SystemLanguage.ChineseTraditional;
        bool japanese = language == SystemLanguage.Japanese;
        var gold = new Color32(237, 198, 120, 255);
        var hintBackdrop = Region("Continue Backdrop", new Vector2(.025f, .02f), new Vector2(.975f, .075f)).gameObject.AddComponent<Image>();
        hintBackdrop.color = new Color32(12, 18, 26, 200);
        hintBackdrop.raycastTarget = false;
        Caption("Continue Hint", new Vector2(.055f, .02f), new Vector2(.945f, .075f), 32, gold, TextAnchor.MiddleCenter)
            .text = chinese ? "轻触继续" : japanese ? "タップして続ける" : "Tap to continue";
    }
}

using UnityEngine;
using UnityEngine.UI;

public partial class ArenaFightOver
{
    private GameObject aiStoryPresentation;

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
        storyBgImage = Region("Generated Illustration", new Vector2(.06f, .43f), new Vector2(.94f, .85f)).gameObject.AddComponent<Image>();
        storyBgImage.raycastTarget = false;
        storyBgImage.preserveAspect = true;
        shortStory = Caption("Story Caption", new Vector2(.08f, .12f), new Vector2(.92f, .4f), 44,
            new Color32(241, 238, 227, 255), TextAnchor.UpperLeft);
        shortStory.resizeTextForBestFit = true;
        shortStory.resizeTextMinSize = 32;
        shortStory.resizeTextMaxSize = 44;

        var language = AIStoryRuntimeContext.GetLanguage();
        bool chinese = language == SystemLanguage.Chinese || language == SystemLanguage.ChineseSimplified || language == SystemLanguage.ChineseTraditional;
        bool japanese = language == SystemLanguage.Japanese;
        var gold = new Color32(237, 198, 120, 255);
        Caption("Story Heading", new Vector2(.08f, .89f), new Vector2(.92f, .95f), 42, gold, TextAnchor.MiddleCenter)
            .text = chinese ? "战斗余韵" : japanese ? "戦いの余韻" : "After the battle";
        Caption("Continue Hint", new Vector2(.08f, .04f), new Vector2(.92f, .09f), 32, gold, TextAnchor.MiddleCenter)
            .text = chinese ? "轻触继续" : japanese ? "タップして続ける" : "Tap to continue";
    }
}

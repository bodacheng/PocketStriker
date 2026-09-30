using System;
using Cysharp.Threading.Tasks;
using ModelView;
using Skill;
using UnityEngine;
using UnityEngine.UI;

public partial class FightPrepareLayer
{
    RectTransform _preparationSkillOverlay, _preparationSkillCard, _preparationSkillViewport;
    Text _preparationSkillCaption, _preparationSkillTitle, _preparationSkillMetadata, _preparationSkillIntro;
    Text _preparationSkillPreviewLabel, _preparationSkillCloseLabel;
    Button _preparationSkillPreviewButton, _preparationSkillCloseButton;
    ScrollRect _preparationSkillScroll;
    SkillConfig _preparationSelectedSkill;
    Action _preparationSkillPreviewAction;

    void BindPreparationSkillSlots(NineForShow slots, DedicatedCameraConnector preview)
    {
        if (slots == null) return;
        slots.AddOnClickToSlots((string recordId) =>
        {
            if (this == null || IsClosing) return;
            var config = SkillConfigTable.GetSkillConfigByRecordId(recordId);
            if (config == null) return;
            ShowPreparationSkillDetails(config, preview == null ? null : (Action)(() =>
            {
                if (this == null || IsClosing || preview == null) return;
                preview.SkillShowRunWithPrepare(config.REAL_NAME).Forget();
            }));
        });
    }

    /// <summary>Also accepts a local preview callback for offline editor fixtures.</summary>
    public void ShowPreparationSkillDetails(SkillConfig config, Action preview)
    {
        if (config == null || IsClosing) return;
        EnsurePreparationSkillDetails();
        _preparationSelectedSkill = config;
        _preparationSkillPreviewAction = preview;
        _preparationSkillOverlay.gameObject.SetActive(true);
        _preparationSkillOverlay.SetAsLastSibling();
        LayoutPreparationSkillDetails();
        _preparationSkillScroll.verticalNormalizedPosition = 1;
    }

    public void HidePreparationSkillDetails()
    {
        _preparationSelectedSkill = null;
        _preparationSkillPreviewAction = null;
        if (_preparationSkillOverlay != null) _preparationSkillOverlay.gameObject.SetActive(false);
    }

    void EnsurePreparationSkillDetails()
    {
        if (_preparationSkillOverlay != null) return;
        var parent = MiddleArea != null ? MiddleArea : (RectTransform)transform;
        _preparationSkillOverlay = Decoration(parent, "PreparationSkillDetails", new Color(0.008f, 0.018f, 0.03f, 0.82f));
        _preparationSkillOverlay.GetComponent<Image>().raycastTarget = true;
        var dismiss = _preparationSkillOverlay.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        dismiss.onClick.AddListener(HidePreparationSkillDetails);

        _preparationSkillCard = Decoration(_preparationSkillOverlay, "PreparationSkillCard", new Color(0.055f, 0.085f, 0.115f, 1));
        // A separate click handler stops card clicks from reaching the scrim.
        var cardImage = _preparationSkillCard.GetComponent<Image>();
        cardImage.raycastTarget = true;
        var cardClicks = _preparationSkillCard.gameObject.AddComponent<Button>();
        cardClicks.targetGraphic = cardImage;
        cardClicks.transition = Selectable.Transition.None;
        cardClicks.navigation = new Navigation { mode = Navigation.Mode.None };
        var accent = Decoration(_preparationSkillCard, "PreparationSkillAccent", PreparationCyan);
        accent.anchorMin = new Vector2(0, 1);
        accent.anchorMax = Vector2.one;
        accent.pivot = new Vector2(0.5f, 1);
        accent.offsetMin = new Vector2(0, -3);
        accent.offsetMax = Vector2.zero;

        var font = arcadeStageNoText.font;
        _preparationSkillCaption = SkillDetailText(_preparationSkillCard, "PreparationSkillCaption", font, 24, PreparationMuted);
        _preparationSkillTitle = SkillDetailText(_preparationSkillCard, "PreparationSkillTitle", font, 36, PreparationInk);
        _preparationSkillMetadata = SkillDetailText(_preparationSkillCard, "PreparationSkillMetadata", font, 26, PreparationCyan);

        _preparationSkillViewport = Decoration(_preparationSkillCard, "PreparationSkillViewport", Color.clear);
        _preparationSkillViewport.GetComponent<Image>().raycastTarget = true;
        _preparationSkillViewport.gameObject.AddComponent<RectMask2D>();
        _preparationSkillIntro = SkillDetailText(_preparationSkillViewport, "PreparationSkillIntro", font, 30, PreparationInk);
        _preparationSkillIntro.alignment = TextAnchor.UpperLeft;
        _preparationSkillIntro.lineSpacing = 1.25f;
        _preparationSkillIntro.verticalOverflow = VerticalWrapMode.Overflow;
        _preparationSkillScroll = _preparationSkillViewport.gameObject.AddComponent<ScrollRect>();
        _preparationSkillScroll.viewport = _preparationSkillViewport;
        _preparationSkillScroll.content = _preparationSkillIntro.rectTransform;
        _preparationSkillScroll.horizontal = false;
        _preparationSkillScroll.vertical = true;
        _preparationSkillScroll.movementType = ScrollRect.MovementType.Clamped;

        _preparationSkillCloseButton = SkillDetailButton(_preparationSkillCard, "PreparationSkillClose", font,
            new Color(0.15f, 0.20f, 0.25f), PreparationInk, out _preparationSkillCloseLabel);
        _preparationSkillCloseButton.onClick.AddListener(HidePreparationSkillDetails);
        _preparationSkillPreviewButton = SkillDetailButton(_preparationSkillCard, "PreparationSkillPreview", font,
            new Color(0.11f, 0.37f, 0.42f), PreparationInk, out _preparationSkillPreviewLabel);
        _preparationSkillPreviewButton.onClick.AddListener(() =>
        {
            var action = _preparationSkillPreviewAction;
            HidePreparationSkillDetails();
            action?.Invoke();
        });
        _preparationSkillOverlay.gameObject.SetActive(false);
    }

    public void LayoutPreparationSkillDetails()
    {
        if (_preparationSkillOverlay == null || _preparationSelectedSkill == null) return;
        var parent = MiddleArea != null ? MiddleArea : (RectTransform)transform;
        if (_preparationSkillOverlay.parent != parent) _preparationSkillOverlay.SetParent(parent, false);
        Stretch(_preparationSkillOverlay);
        float width = Mathf.Min(900, parent.rect.width - 64);
        float height = Mathf.Min(650, parent.rect.height - 40);
        if (width <= 0 || height <= 0) return;
        Place(_preparationSkillCard, (parent.rect.width - width) * 0.5f, (parent.rect.height - height) * 0.5f, width, height);
        float contentWidth = width - 64;
        Place(_preparationSkillCaption.rectTransform, 32, 24, contentWidth, 34);
        Place(_preparationSkillTitle.rectTransform, 32, 64, contentWidth, 100);
        Place(_preparationSkillMetadata.rectTransform, 32, 170, contentWidth, 82);
        float viewportHeight = Mathf.Max(44, height - 376);
        Place(_preparationSkillViewport, 32, 266, contentWidth, viewportHeight);

        _preparationSkillCaption.text = SkillDescription.DetailsTitle;
        _preparationSkillTitle.text = SkillDescription.GetName(_preparationSelectedSkill);
        _preparationSkillMetadata.text = SkillDescription.GetMetadata(_preparationSelectedSkill);
        _preparationSkillIntro.text = SkillDescription.GetIntro(_preparationSelectedSkill);
        _preparationSkillPreviewLabel.text = SkillDescription.PreviewLabel;
        _preparationSkillCloseLabel.text = SkillDescription.CloseLabel;
        _preparationSkillPreviewButton.interactable = _preparationSkillPreviewAction != null;

        var content = _preparationSkillIntro.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, viewportHeight);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportHeight, _preparationSkillIntro.preferredHeight));
        float buttonWidth = (contentWidth - 20) * 0.5f;
        Place((RectTransform)_preparationSkillCloseButton.transform, 32, height - 96, buttonWidth, 68);
        Place((RectTransform)_preparationSkillPreviewButton.transform, 52 + buttonWidth, height - 96, buttonWidth, 68);
    }

    static Text SkillDetailText(RectTransform parent, string name, Font font, int size, Color color)
    {
        var text = Label(parent, name, font);
        text.fontSize = size;
        text.color = color;
        text.resizeTextForBestFit = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static Button SkillDetailButton(RectTransform parent, string name, Font font, Color fill, Color ink, out Text label)
    {
        var rect = Decoration(parent, name, fill);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1);
        colors.pressedColor = new Color(0.75f, 0.85f, 0.9f, 1);
        colors.disabledColor = new Color(0.45f, 0.5f, 0.55f, 1);
        button.colors = colors;
        label = SkillDetailText(rect, name + "Label", font, 28, ink);
        label.alignment = TextAnchor.MiddleCenter;
        Stretch(label.rectTransform, 8);
        return button;
    }
}

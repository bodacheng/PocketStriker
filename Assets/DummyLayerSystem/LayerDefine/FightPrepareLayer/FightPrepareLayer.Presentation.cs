using UnityEngine;
using UnityEngine.UI;

public partial class FightPrepareLayer
{
    static readonly Color PreparationInk = new Color(0.91f, 0.95f, 0.97f);
    static readonly Color PreparationMuted = new Color(0.57f, 0.66f, 0.73f);
    static readonly Color PreparationCyan = new Color(0.36f, 0.79f, 0.85f);
    bool _layingOutPreparation;
    bool _preparationStyled;
    bool _preparationLayoutReady;
    RectTransform _preparationPreview;
    Text _preparationSkillLabel;
    Text _preparationSkillHint;
    bool _preparationButtonsStyled, _preparationBattlefieldVisible;
    Vector2 _preparationRootSize, _preparationTopSize, _preparationMiddleSize;
    SystemLanguage _preparationLanguage;
    int _preparationHeroChildren, _preparationEnemyChildren;
    RectTransform _preparationFight, _preparationEdit, _preparationSelectors;
    Vector2 _preparationFightPosition, _preparationEditPosition, _preparationSelectorPosition;

    protected override void OnAreasResized()
    {
        RefreshPreparationLayout();
        LayoutPreparationSkillDetails();
    }

    // Mode clips still own visibility. Resolve their legacy position curves
    // after animation sampling; button skins own their feedback.
    void LateUpdate()
    {
        if (connectorE != null)
        {
            RefreshGroupPreparationIfNeeded();
            return;
        }
        if (connectorE != null || TopArea == null || MiddleArea == null
            || myTeamShowT == null || enemyTeamShowT == null) return;
        if (!_preparationLayoutReady || ((RectTransform)transform).rect.size != _preparationRootSize
            || TopArea.rect.size != _preparationTopSize || MiddleArea.rect.size != _preparationMiddleSize
            || AppSetting.Value.Language != _preparationLanguage
            || (battleGroundSwitch != null && battleGroundSwitch.gameObject.activeSelf) != _preparationBattlefieldVisible
            || myTeamShowT.childCount != _preparationHeroChildren || enemyTeamShowT.childCount != _preparationEnemyChildren)
        {
            RefreshPreparationLayout();
            return;
        }
        // Only these positions are written by the old mode clips. Stable panels,
        // text and grids need no layout rebuild or component searches each frame.
        if (_preparationFight != null) _preparationFight.anchoredPosition = _preparationFightPosition;
        if (_preparationEdit != null) _preparationEdit.anchoredPosition = _preparationEditPosition;
        if (_preparationSelectors != null) _preparationSelectors.anchoredPosition = _preparationSelectorPosition;
        RefreshPreparationHint();
    }

    public void RefreshPreparationLayout()
    {
        ApplyPreparationButtons();
        if (connectorE != null)
        {
            RefreshGroupPreparationLayout();
            return;
        }
        if (_layingOutPreparation || connectorE != null || TopArea == null || MiddleArea == null
            || connector == null || nineForShow == null || enemyTeamShowT == null || myTeamShowT == null) return;
        _layingOutPreparation = true;
        try
        {
            var root = (RectTransform)transform;
            float width = Mathf.Min(root.rect.width - 64, 1080);
            if (width <= 0 || TopArea.rect.height <= 0 || MiddleArea.rect.height <= 0) return;
            float left = (root.rect.width - width) * 0.5f;
            var enemyPanel = (RectTransform)enemyTeamShowT.parent;
            var playerPanel = (RectTransform)myTeamShowT.parent;
            var teams = (RectTransform)enemyPanel.parent;
            var action = teams.Find("mid") as RectTransform;

            if (!_preparationStyled)
            {
                _preparationStyled = true;
                var backdrop = Decoration(root, "PreparationBackdrop", new Color(0.025f, 0.045f, 0.065f, 0.58f));
                Stretch(backdrop);
                backdrop.SetAsFirstSibling();
                _preparationPreview = Decoration(TopArea, "PreparationPreview", new Color(0.045f, 0.075f, 0.105f, 0.72f));
                _preparationPreview.SetAsFirstSibling();
                _preparationSkillLabel = Label(TopArea, "PreparationSkills", arcadeStageNoText.font);
                _preparationSkillHint = Label(TopArea, "PreparationSkillHint", arcadeStageNoText.font);
                Panel(enemyPanel, new Color(0.055f, 0.075f, 0.10f, 0.88f), new Color(0.75f, 0.36f, 0.33f, 0.7f));
                Panel(playerPanel, new Color(0.045f, 0.085f, 0.115f, 0.9f), PreparationCyan * new Color(1, 1, 1, 0.65f));
                var group = teams.GetComponent<LayoutGroup>();
                if (group != null) group.enabled = false;
                if (teamEditIndicatorText.transform.parent != playerPanel)
                    teamEditIndicatorText.transform.SetParent(playerPanel, false);
                foreach (var shadow in arcadeStageNoText.GetComponents<Shadow>()) shadow.enabled = false;
                StyleSkillFrames();
            }

            // The stage heading and reward line belong above the opponent preview.
            var title = arcadeStageNoText.rectTransform;
            if (title.parent != TopArea) title.SetParent(TopArea, false);
            Place(title, left, 22, width - 244, 74);
            arcadeStageNoText.color = PreparationInk;
            arcadeStageNoText.fontSize = 40;
            arcadeStageNoText.resizeTextForBestFit = false;
            arcadeStageNoText.alignment = TextAnchor.MiddleLeft;
            arcadeStageNoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            arcadeStageNoText.verticalOverflow = VerticalWrapMode.Truncate;
            var back = (RectTransform)toArcadeFrontBtn.transform;
            if (back.parent != TopArea) back.SetParent(TopArea, false);
            Place(back, left + width - 212, 24, 212, 76);
            LayoutPreparationRewards(left);

            float previewHeight = Mathf.Max(240, TopArea.rect.height - 192);
            Place(_preparationPreview, left, 176, width, previewHeight);
            float gemSize = Mathf.Min(252, width * 0.30f, previewHeight - 106);
            float gridLeft = left + width - gemSize - 32;
            float gridTop = 176 + (previewHeight - gemSize) * 0.5f + 12;
            var gridRoot = (RectTransform)nineForShow.transform.parent;
            Place(gridRoot, gridLeft, gridTop, gemSize, gemSize);
            Stretch((RectTransform)nineForShow.transform);
            var grid = nineForShow.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.spacing = Vector2.one * 6;
                grid.cellSize = Vector2.one * ((gemSize - 12) / 3);
            }
            _preparationSkillLabel.text = Translate.Get("SkillGem");
            Place(_preparationSkillLabel.rectTransform, gridLeft, gridTop - 58, gemSize, 44);
            _preparationSkillLabel.color = PreparationMuted;
            _preparationSkillLabel.fontSize = 26;
            _preparationSkillHint.text = SkillDescription.TapHint;
            Place(_preparationSkillHint.rectTransform, gridLeft - 12, gridTop + gemSize + 8, gemSize + 24, 32);
            _preparationSkillHint.color = PreparationMuted;
            _preparationSkillHint.fontSize = 20;
            _preparationSkillHint.alignment = TextAnchor.MiddleCenter;
            float modelWidth = Mathf.Min(570, width - gemSize - 80);
            Place((RectTransform)connector.transform, left + 24 + (width - gemSize - 80 - modelWidth) * 0.5f,
                190, modelWidth, previewHeight - 30);
            connector.EnableUIPresentation();
            if (connector.transform.Find("touchOperationRange") is RectTransform touch) Stretch(touch);

            Stretch(teams);
            // Both rosters keep their own header, portraits and optional arena motto.
            float compact = Mathf.Min(1, MiddleArea.rect.height / 1050);
            float enemyHeight = 288 * compact;
            float playerTop = enemyHeight + 28;
            float playerHeight = 302 * compact;
            Place(enemyPanel, left, 16, width, enemyHeight);
            Place(playerPanel, left, playerTop + 16, width, playerHeight);
            LayoutPreparationRoster(enemyTeamShowT, team2Name, team2OneWord, false, width, enemyHeight);
            LayoutPreparationRoster(myTeamShowT, team1Name, team1OneWord, true, width, playerHeight);
            Place((RectTransform)editTeamButton.transform, width - 232, 16, 200, 68);
            Place(teamEditIndicatorText.rectTransform, 32, playerHeight - 52, width - 64, 40);
            teamEditIndicatorText.fontSize = 24;
            teamEditIndicatorText.color = PreparationMuted;
            RefreshPreparationHint();
            var arrow = editTeamButton.transform.Find("arrow");
            if (arrow != null) Place((RectTransform)arrow, -46, 8, 44, 44);

            float actionTop = playerTop + playerHeight + 50;
            float actionHeight = MiddleArea.rect.height - actionTop - 12;
            if (action != null)
            {
                Place(action, left, actionTop, width, actionHeight);
                var fight = (RectTransform)beginFight.transform;
                fight.anchorMin = fight.anchorMax = fight.pivot = new Vector2(0.5f, 0.5f);
                fight.anchoredPosition = Vector2.zero;
                fight.sizeDelta = new Vector2(416, 104);
                fight.localScale = Vector3.one;
                var selectors = action.Find("V") as RectTransform;
                if (selectors != null)
                {
                    var layout = selectors.GetComponent<LayoutGroup>();
                    if (layout != null) layout.enabled = false;
                    bool showBattlefield = battleGroundSwitch != null && battleGroundSwitch.gameObject.activeSelf;
                    float selectorHeight = showBattlefield ? 240 : 88;
                    Place(selectors, width - 242, Mathf.Max(0, (actionHeight - selectorHeight) * 0.5f), 220, selectorHeight);
                    Place((RectTransform)fightModeSwitch.transform, 0, 0, 220, 88);
                    if (battleGroundSwitch != null) Place((RectTransform)battleGroundSwitch.transform, 0, 100, 220, 130);
                }
            }
            var exParent = enemyDoubleExModeFlg != null ? enemyDoubleExModeFlg.transform.parent as RectTransform : null;
            if (exParent != null)
            {
                Place(exParent, width - 230, 20, 190, 56);
                foreach (RectTransform badge in exParent) Stretch(badge);
                StylePreparationEnergyFlag(enemyDoubleExModeFlg, "StageEnergyDouble", new Color(1f, .86f, .4f));
                StylePreparationEnergyFlag(enemyInfiniteExModeFlg, "StageEnergyUnlimited", new Color(1f, .55f, .7f));
            }
            _preparationRootSize = root.rect.size;
            _preparationTopSize = TopArea.rect.size;
            _preparationMiddleSize = MiddleArea.rect.size;
            _preparationLanguage = AppSetting.Value.Language;
            _preparationBattlefieldVisible = battleGroundSwitch != null && battleGroundSwitch.gameObject.activeSelf;
            _preparationHeroChildren = myTeamShowT.childCount;
            _preparationEnemyChildren = enemyTeamShowT.childCount;
            _preparationFight = (RectTransform)beginFight.transform;
            _preparationEdit = (RectTransform)editTeamButton.transform;
            _preparationSelectors = action != null ? action.Find("V") as RectTransform : null;
            _preparationFightPosition = _preparationFight.anchoredPosition;
            _preparationEditPosition = _preparationEdit.anchoredPosition;
            if (_preparationSelectors != null) _preparationSelectorPosition = _preparationSelectors.anchoredPosition;
            _preparationLayoutReady = true;
            LayoutPreparationSkillDetails();
        }
        finally { _layingOutPreparation = false; }
    }

    void ApplyPreparationButtons()
    {
        if (_preparationButtonsStyled || beginFight == null || editTeamButton == null || fightModeSwitch == null) return;
        beginFight.ApplyPreparationSkin();
        fightModeSwitch.ApplyPreparationSkin();
        var label = editTeamButton.transform.Find("text")?.GetComponent<Text>();
        if (label == null) return;
        var skin = PreparationButtonSkin.Apply(editTeamButton, label, PreparationCyan, false, 32);
        if (skin == null) return;
        foreach (var shadow in editTeamButton.GetComponentsInChildren<Shadow>(true)) shadow.enabled = false;
        var oldBackground = editTeamButton.GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
        var oldFrame = editTeamButton.transform.Find("Frame");
        if (oldFrame != null)
            foreach (var image in oldFrame.GetComponentsInChildren<Image>(true)) image.enabled = false;
        if (connectorE != null)
        {
            var groupEdit = (RectTransform)editTeamButton.transform;
            groupEdit.sizeDelta = new Vector2(200, 68);
            // The legacy roster is scaled down to fit many units. Keep its edit
            // action at the same readable size as the standard preparation page.
            var rootScale = transform.lossyScale;
            var parentScale = groupEdit.parent.lossyScale;
            groupEdit.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.0001f ? rootScale.x / parentScale.x : 1,
                Mathf.Abs(parentScale.y) > 0.0001f ? rootScale.y / parentScale.y : 1, 1);
            var panelCorners = new Vector3[4];
            ((RectTransform)groupEdit.parent).GetWorldCorners(panelCorners);
            var panelTop = transform.InverseTransformPoint(panelCorners[1]).y;
            groupEdit.pivot = new Vector2(0.5f, 0.5f);
            groupEdit.position = transform.TransformPoint(new Vector3(
                ((RectTransform)transform).rect.xMin + 132, panelTop + 46, 0));
            ((RectTransform)fightModeSwitch.transform).sizeDelta = new Vector2(220, 88);
        }
        if (toArcadeFrontBtn != null)
        {
            var backLabel = toArcadeFrontBtn.GetComponentInChildren<Text>(true);
            if (backLabel != null && PreparationButtonSkin.Apply(toArcadeFrontBtn, backLabel, PreparationMuted, false, 26) != null)
            {
                foreach (var image in toArcadeFrontBtn.GetComponentsInChildren<Image>(true))
                    if (image.name != "PreparationFill" && image.name != "PreparationOutline") image.enabled = false;
                PreparationButtonSkin.Fit(backLabel.rectTransform, 10, 8);
                if (connectorE != null) ((RectTransform)toArcadeFrontBtn.transform).sizeDelta = new Vector2(212, 76);
            }
        }
        _preparationButtonsStyled = true;
    }

    void RefreshPreparationHint()
    {
        teamEditIndicatorText.enabled = editTeamButton.gameObject.activeInHierarchy
            && string.IsNullOrEmpty(team1OneWord.text);
    }

    static void StylePreparationEnergyFlag(GameObject flag, string languageCode, Color accent)
    {
        if (flag == null) return;
        var image = flag.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(accent.r, accent.g, accent.b, .7f);
            image.raycastTarget = false;
        }
        foreach (var converter in flag.GetComponentsInChildren<LanguageConverter>(true))
            converter.ChangeAtOnce(languageCode);
        foreach (var text in flag.GetComponentsInChildren<Text>(true))
        {
            PreparationButtonSkin.Fit(text.rectTransform, 12, 6);
            text.color = accent;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Normal;
            text.resizeTextForBestFit = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            foreach (var shadow in text.GetComponents<Shadow>()) shadow.enabled = false;
        }
    }

    void LayoutPreparationRewards(float left)
    {
        var reward = (RectTransform)rewardUI.transform;
        if (reward.parent != TopArea) reward.SetParent(TopArea, false);
        Place(reward, left, 108, 460, 50);
        var layout = reward.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        int index = 0;
        foreach (RectTransform row in reward)
        {
            Place(row, index++ * 240, 0, 220, 50);
            foreach (var count in row.GetComponentsInChildren<Text>(true))
            {
                PlaceRewardItem(count.rectTransform, 52, 126, 50);
                count.fontSize = 28;
                count.resizeTextForBestFit = false;
                count.alignment = TextAnchor.MiddleLeft;
            }
            if (row.Find("icon") is RectTransform icon) PlaceRewardItem(icon, 0, 40, 40);
            if (row.Find("GotMark") is RectTransform mark) PlaceRewardItem(mark, 188, 24, 36);
        }
    }

    static void LayoutPreparationRoster(RectTransform row, Text name, Text motto, bool player, float width, float height)
    {
        Place(name.rectTransform, 32, 16, width - 300, 56);
        name.fontSize = 30;
        name.color = player ? PreparationCyan : PreparationMuted;
        name.resizeTextForBestFit = false;
        name.alignment = TextAnchor.MiddleLeft;
        float rowWidth = width - 64;
        float iconSize = Mathf.Min(player ? 164 : 154, height - 124);
        int count = 0;
        foreach (Transform child in row) if (child.gameObject.activeSelf) count++;
        if (count > 0) iconSize = Mathf.Min(iconSize, (rowWidth - (count - 1) * 20) / count);
        Place(row, 32, 88, rowWidth, iconSize);
        var group = row.GetComponent<HorizontalLayoutGroup>();
        if (group != null)
        {
            group.enabled = true;
            group.spacing = 20;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = group.childControlHeight = false;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
        }
        foreach (RectTransform child in row)
        {
            if (!child.gameObject.activeSelf) continue;
            var size = new Vector2(iconSize, iconSize);
            bool resized = child.sizeDelta != size;
            child.sizeDelta = size;
            var hero = child.GetComponent<HeroIcon>();
            hero?.ApplyPreparationStyle();
            if (resized) hero?.RefreshPresentationSize();
        }
        Place(motto.rectTransform, 32, height - 42, rowWidth, 34);
        motto.fontSize = 22;
        motto.color = PreparationMuted;
        motto.alignment = TextAnchor.MiddleLeft;
    }

    void StyleSkillFrames()
    {
        nineForShow.StylePreparationSlots(new Color(0.50f, 0.66f, 0.75f, 0.32f));
    }

    static void Panel(RectTransform root, Color fill, Color accent)
    {
        if (root.Find("bg") is RectTransform oldBackground) oldBackground.gameObject.SetActive(false);
        var image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        image.color = fill;
        image.raycastTarget = false;
        var line = Decoration(root, "PreparationAccent", accent);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = Vector2.one;
        line.pivot = new Vector2(0.5f, 1);
        line.offsetMin = new Vector2(0, -2);
        line.offsetMax = Vector2.zero;
        line.SetAsFirstSibling();
    }

    static RectTransform Decoration(RectTransform parent, string name, Color color)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return (RectTransform)node.transform;
    }

    static Text Label(RectTransform parent, string name, Font font)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        var text = node.GetComponent<Text>();
        text.font = font;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        return text;
    }

    static void Place(RectTransform target, float left, float top, float width, float height)
    {
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0, 1);
        target.anchoredPosition = new Vector2(left, -top);
        target.sizeDelta = new Vector2(width, height);
        target.localScale = Vector3.one;
    }

    static void Stretch(RectTransform target, float inset = 0)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.one * inset;
        target.offsetMax = Vector2.one * -inset;
        target.localScale = Vector3.one;
    }
}

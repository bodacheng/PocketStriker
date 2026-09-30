using UnityEngine;
using UnityEngine.UI;

public partial class FightPrepareLayer
{
    RectTransform _groupHeader, _groupLeft, _groupRight, _groupPreview, _groupActions;
    GameObject _groupLegacyPlayer, _groupLegacyEnemy, _groupLegacyAction;
    Text _groupCountTitle, _groupSkillHint, _groupVersus;
    Rect _groupLastSafe;
    int _groupHeroChildren = -1, _groupEnemyChildren = -1;
    SystemLanguage _groupLanguage;
    bool _groupLayoutBusy;

    void RefreshGroupPreparationIfNeeded()
    {
        if (_groupLayoutBusy || myTeamShowT == null || enemyTeamShowT == null) return;
        if (_groupHeader == null || GroupSafeRect() != _groupLastSafe
            || _groupLanguage != AppSetting.Value.Language
            || _groupHeroChildren != myTeamShowT.childCount || _groupEnemyChildren != enemyTeamShowT.childCount)
            RefreshGroupPreparationLayout();
    }

    // The group prefab has no UILayer top/middle/bottom regions. Its portrait
    // layout is resolved directly against the safe area, reserving the main bar.
    public void RefreshGroupPreparationLayout()
    {
        if (_groupLayoutBusy || connectorE == null || connector == null || nineForShow == null
            || nineForShowE == null || myTeamShowT == null || enemyTeamShowT == null) return;
        _groupLayoutBusy = true;
        try
        {
            var root = (RectTransform)transform;
            var safe = GroupSafeRect();
            float width = Mathf.Min(safe.width - 64, 1080);
            if (width <= 0 || safe.height <= 0) return;
            float u = Mathf.Min(width / 1080f, 1);
            float left = safe.center.x - width * 0.5f - root.rect.xMin;
            float top = root.rect.yMax - safe.yMax;
            float headerHeight = 184 * u;
            float footerHeight = 312 * u;
            float navigationHeight = 180 * u;
            float bodyHeight = Mathf.Max(460 * u, safe.height - headerHeight - footerHeight - navigationHeight - 32 * u);
            float bodyTop = top + headerHeight;
            float footerTop = bodyTop + bodyHeight + 16 * u;
            float railWidth = 184 * u;
            float centerLeft = left + railWidth + 24 * u;
            float centerWidth = width - 2 * railWidth - 48 * u;

            if (_groupHeader == null)
            {
                // Group mode uses explicit layout; the old clips position all
                // controls at the bottom of the screen and would undo it.
                if (layerAnimator != null) layerAnimator.enabled = false;
                _groupLegacyPlayer = myTeamShowT.parent.gameObject;
                _groupLegacyEnemy = enemyTeamShowT.parent.gameObject;
                _groupLegacyAction = beginFight.transform.parent.gameObject;
                _groupHeader = Decoration(root, "GroupPreparationHeader", new Color(0.025f, 0.065f, 0.095f, 0.68f));
                _groupPreview = Decoration(root, "GroupPreparationPreview", new Color(0.035f, 0.08f, 0.12f, 0.58f));
                _groupLeft = Decoration(root, "GroupPlayerRail", new Color(0.035f, 0.09f, 0.12f, 0.82f));
                _groupRight = Decoration(root, "GroupEnemyRail", new Color(0.075f, 0.065f, 0.09f, 0.82f));
                _groupActions = Decoration(root, "GroupPreparationActions", new Color(0.025f, 0.065f, 0.095f, 0.8f));
                _groupCountTitle = Label(_groupActions, "GroupCountTitle", arcadeStageNoText.font);
                _groupSkillHint = Label(_groupPreview, "GroupSkillHint", arcadeStageNoText.font);
                _groupVersus = Label(_groupPreview, "GroupVersus", arcadeStageNoText.font);
                MoveGroup(arcadeStageNoText.transform, _groupHeader);
                MoveGroup(toArcadeFrontBtn.transform, _groupHeader);
                MoveGroup(rewardUI.transform, _groupHeader);
                MoveGroup(editTeamButton.transform, _groupActions);
                MoveGroup(beginFight.transform, _groupActions);
                MoveGroup(countSet1.transform, _groupActions);
                MoveGroup(countSet2.transform, _groupActions);
                MoveGroup(countSet3.transform, _groupActions);
                MoveGroup(connector.transform, _groupPreview);
                MoveGroup(connectorE.transform, _groupPreview);
                MoveGroup(nineForShow.transform.parent, _groupPreview);
                MoveGroup(nineForShowE.transform.parent, _groupPreview);
                // Redundant legacy labels/backgrounds stay out of the model area.
                if (view2D != null) view2D.gameObject.SetActive(false);
                if (team1Flg != null) team1Flg.gameObject.SetActive(false);
                if (team2Flg != null) team2Flg.gameObject.SetActive(false);
                if (teamEditIndicator != null) teamEditIndicator.SetActive(false);
                nineForShow.StylePreparationSlots(new Color(0.50f, 0.66f, 0.75f, 0.24f));
                nineForShowE.StylePreparationSlots(new Color(0.50f, 0.66f, 0.75f, 0.24f));
            }
            Place(_groupHeader, left, top + 16 * u, width, headerHeight - 28 * u);
            Place(_groupLeft, left, bodyTop, railWidth, bodyHeight);
            Place(_groupRight, left + width - railWidth, bodyTop, railWidth, bodyHeight);
            Place(_groupPreview, centerLeft, bodyTop, centerWidth, bodyHeight);
            Place(_groupActions, left, footerTop, width, footerHeight);

            Place(arcadeStageNoText.rectTransform, 24 * u, 12 * u, width - 212 - 68 * u, 70 * u);
            GroupText(arcadeStageNoText, 36, PreparationInk, TextAnchor.MiddleLeft);
            Place((RectTransform)toArcadeFrontBtn.transform, width - 212 - 20 * u, 16 * u, 212, 76);
            LayoutGroupRewards(u);
            LayoutGroupRail(_groupLeft, myTeamShowT, team1Name, team1WholeCount, true, railWidth, bodyHeight, u);
            LayoutGroupRail(_groupRight, enemyTeamShowT, team2Name, team2WholeCount, false, railWidth, bodyHeight, u);
            _groupLegacyPlayer.SetActive(false);
            _groupLegacyEnemy.SetActive(false);
            if (_groupLegacyAction != gameObject) _groupLegacyAction.SetActive(false);
            foreach (var badge in new[] { modeFlgR, modeFlgM, modeFlgE, modeFlgG, enemyDoubleExModeFlg, enemyInfiniteExModeFlg })
                if (badge != null) badge.SetActive(false);
            if (battleGroundSwitch != null) battleGroundSwitch.gameObject.SetActive(false);

            float columnWidth = (centerWidth - 16 * u) * 0.5f;
            float gemSize = Mathf.Min(248 * u, columnWidth - 24 * u);
            float modelHeight = Mathf.Min(680 * u, bodyHeight - gemSize - 156 * u);
            float modelTop = 32 * u + Mathf.Max(0, (bodyHeight - modelHeight - gemSize - 156 * u) * 0.3f);
            Place((RectTransform)connector.transform, 8 * u, modelTop, columnWidth - 8 * u, modelHeight);
            Place((RectTransform)connectorE.transform, columnWidth + 16 * u, modelTop, columnWidth - 8 * u, modelHeight);
            connector.EnableUIPresentation();
            connectorE.EnableUIPresentation();
            if (connector.transform.Find("touchOperationRange") is RectTransform heroTouch) Stretch(heroTouch);
            if (connectorE.transform.Find("touchOperationRange") is RectTransform enemyTouch) Stretch(enemyTouch);
            float gridTop = modelTop + modelHeight + 28 * u;
            LayoutGroupSkills(nineForShow, (columnWidth - gemSize) * 0.5f, gridTop, gemSize, u);
            LayoutGroupSkills(nineForShowE, columnWidth + 16 * u + (columnWidth - gemSize) * 0.5f, gridTop, gemSize, u);
            _groupVersus.text = "VS";
            GroupText(_groupVersus, 30, PreparationMuted, TextAnchor.MiddleCenter);
            Place(_groupVersus.rectTransform, centerWidth * 0.5f - 36 * u, modelTop + modelHeight * 0.46f, 72 * u, 48 * u);
            _groupSkillHint.text = SkillDescription.TapHint;
            GroupText(_groupSkillHint, 20, PreparationMuted, TextAnchor.MiddleCenter);
            Place(_groupSkillHint.rectTransform, 8 * u, gridTop + gemSize + 12 * u, centerWidth - 16 * u, 42 * u);

            _groupCountTitle.text = GroupCaption("Units per team", "チームごとの人数", "每队人数");
            GroupText(_groupCountTitle, 26, PreparationMuted, TextAnchor.MiddleCenter);
            Place(_groupCountTitle.rectTransform, 24 * u, 8 * u, width - 48 * u, 46 * u);
            float optionWidth = 212 * u, optionGap = 20 * u;
            float optionLeft = (width - optionWidth * 3 - optionGap * 2) * 0.5f;
            LayoutGroupCountOption(countSet1, groupCount1, countSelectedFrame1, optionLeft, optionWidth, u, CommonSetting.GangbangModeMaxUnitPerTeam1);
            LayoutGroupCountOption(countSet2, groupCount2, countSelectedFrame2, optionLeft + optionWidth + optionGap, optionWidth, u, CommonSetting.GangbangModeMaxUnitPerTeam2);
            LayoutGroupCountOption(countSet3, groupCount3, countSelectedFrame3, optionLeft + 2 * (optionWidth + optionGap), optionWidth, u, CommonSetting.GangbangModeMaxUnitPerTeam3);
            Place((RectTransform)editTeamButton.transform, 24 * u, 168 * u, 200, 68);
            Place((RectTransform)beginFight.transform, (width - 416) * 0.5f, 154 * u, 416, 104);
            var fightLabel = beginFight.GetComponentInChildren<Text>(true);
            if (fightLabel != null) fightLabel.text = "FIGHT";
            if (editTeamButton.transform.Find("arrow") is Transform arrow) arrow.gameObject.SetActive(false);
            if (fightModeSwitch != null) fightModeSwitch.gameObject.SetActive(false);
            _groupLastSafe = safe;
            _groupLanguage = AppSetting.Value.Language;
            _groupHeroChildren = myTeamShowT.childCount;
            _groupEnemyChildren = enemyTeamShowT.childCount;
            LayoutPreparationSkillDetails();
        }
        finally { _groupLayoutBusy = false; }
    }

    void LayoutGroupRail(RectTransform panel, RectTransform content, Text title, Text total, bool player, float width, float height, float u)
    {
        var accent = player ? PreparationCyan : new Color(0.86f, 0.54f, 0.50f);
        MoveGroup(title.transform, panel);
        MoveGroup(total.transform, panel);
        title.gameObject.SetActive(true);
        total.gameObject.SetActive(true);
        title.text = Translate.Get(player ? "Player" : "Enemy");
        GroupText(title, 28, accent, TextAnchor.MiddleCenter);
        GroupText(total, 26, PreparationInk, TextAnchor.MiddleCenter);
        Place(title.rectTransform, 0, 8 * u, width, 48 * u);
        Place(total.rectTransform, 0, 54 * u, width, 40 * u);
        var viewport = panel.Find("RosterViewport") as RectTransform;
        if (viewport == null)
        {
            viewport = Decoration(panel, "RosterViewport", Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
        }
        Place(viewport, 0, 106 * u, width, height - 114 * u);
        MoveGroup(content, viewport);
        foreach (var layout in content.GetComponents<LayoutGroup>()) layout.enabled = false;
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
        float portrait = 136 * u, step = 220 * u;
        int active = 0;
        foreach (RectTransform child in content)
        {
            if (!child.gameObject.activeSelf) continue;
            Place(child, (width - portrait) * 0.5f, active++ * step + 8 * u, portrait, portrait);
            var icon = child.GetComponent<GangbangHeroIcon>();
            icon?.ApplyVerticalPreparationStyle(portrait, width - 8 * u, arcadeStageNoText.font, accent);
        }
        Place(content, 0, 0, width, Mathf.Max(viewport.rect.height, active * step));
    }

    static void LayoutGroupSkills(NineForShow nine, float left, float top, float size, float u)
    {
        Place((RectTransform)nine.transform.parent, left, top, size, size);
        Stretch((RectTransform)nine.transform);
        var grid = nine.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        grid.spacing = Vector2.one * 6 * u;
        grid.cellSize = Vector2.one * ((size - 12 * u) / 3);
    }

    static void LayoutGroupCountOption(BOButton button, Text label, GameObject frame, float left, float width, float u, int count)
    {
        Place((RectTransform)button.transform, left, 64 * u, width, 68 * u);
        foreach (var text in button.GetComponentsInChildren<Text>(true)) if (text != label) text.enabled = false;
        label.transform.SetParent(button.transform, false);
        label.text = count.ToString();
        foreach (var converter in label.GetComponents<LanguageConverter>()) converter.languageCode = string.Empty;
        var skin = button.GetComponent<PreparationButtonSkin>();
        if (skin == null)
        {
            foreach (var image in button.GetComponentsInChildren<Image>(true)) image.enabled = false;
            PreparationButtonSkin.Apply(button, label, PreparationCyan, false, 32);
        }
        PreparationButtonSkin.Fit(label.rectTransform, 8, 4);
        foreach (var shadow in label.GetComponents<Shadow>()) shadow.enabled = false;
        if (frame == null) return;
        frame.transform.SetParent(button.transform, false);
        Stretch((RectTransform)frame.transform, -3);
        var outline = frame.GetComponent<Image>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonOutline");
            outline.type = Image.Type.Sliced;
            outline.fillCenter = false;
            outline.color = new Color(0.90f, 0.76f, 0.38f);
            outline.raycastTarget = false;
            foreach (var shadow in outline.GetComponents<Shadow>()) shadow.enabled = false;
        }
        label.transform.SetAsLastSibling();
    }

    void LayoutGroupRewards(float u)
    {
        var reward = (RectTransform)rewardUI.transform;
        Place(reward, 24 * u, 92 * u, 480 * u, 48 * u);
        var layout = reward.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        int index = 0;
        foreach (RectTransform row in reward)
        {
            Place(row, index++ * 240 * u, 0, 220 * u, 48 * u);
            foreach (var text in row.GetComponentsInChildren<Text>(true))
            {
                PlaceRewardItem(text.rectTransform, 50 * u, 138 * u, 48 * u);
                GroupText(text, 26, PreparationInk, TextAnchor.MiddleLeft);
            }
            if (row.Find("icon") is RectTransform icon) PlaceRewardItem(icon, 0, 38 * u, 38 * u);
            if (row.Find("GotMark") is RectTransform mark) PlaceRewardItem(mark, 190 * u, 24 * u, 32 * u);
        }
    }

    static void MoveGroup(Transform child, RectTransform parent)
    {
        if (child.parent != parent) child.SetParent(parent, false);
        child.localScale = Vector3.one;
    }

    static void GroupText(Text text, int size, Color color, TextAnchor alignment)
    {
        foreach (var converter in text.GetComponents<LanguageConverter>()) converter.languageCode = string.Empty;
        text.fontSize = size;
        text.resizeTextForBestFit = false;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        foreach (var shadow in text.GetComponents<Shadow>()) shadow.enabled = false;
    }

    Rect GroupSafeRect()
    {
        var root = (RectTransform)transform;
        var bounds = root.rect;
        var safe = PosCal.SafeAreaRect;
        if (safe == null || root == safe || root.IsChildOf(safe)) return bounds;
        var corners = new Vector3[4];
        safe.GetWorldCorners(corners);
        var min = root.InverseTransformPoint(corners[0]);
        var max = root.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Max(bounds.xMin, min.x), Mathf.Max(bounds.yMin, min.y),
            Mathf.Min(bounds.xMax, max.x), Mathf.Min(bounds.yMax, max.y));
    }

    static string GroupCaption(string en, string jp, string ch)
    {
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.Japanese: return jp;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return ch;
            default: return en;
        }
    }
}

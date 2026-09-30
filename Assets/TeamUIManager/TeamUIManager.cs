using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace FightScene
{
    public partial class TeamUIManager : MonoBehaviour
    {
        [SerializeField] MobileInputsManager inputsManager;
        [SerializeField] RectTransform sideIconsContainer;
        [SerializeField] RectTransform _targetCanvasT;
        [SerializeField] SideUnitIcon unitIconPrefab;
        [SerializeField] Text rotationModeHitCombo;
        [SerializeField] Animation comboTextAnim;
        [SerializeField] AutoSwitch teamAutoSwitch;
        [SerializeField] RectTransform selectedFrame;
        [SerializeField] int barPosUpdateInterval = 2;
        [SerializeField] int teamIndicatorCloseDelay = 5;
        
        public AutoSwitch AutoSwitch => teamAutoSwitch;
        public RectTransform SideIconsContainer => sideIconsContainer;
        public RectTransform SelectedFrame => selectedFrame;
        public TeamMode TeamMode { get; set; }
        public TeamConfig TeamConfig { get; set; }
        public readonly IDictionary<Data_Center, SideUnitIcon> UnitIconDic = new Dictionary<Data_Center, SideUnitIcon>();
        private IDisposable _barPosUpdate;
        private IDisposable _teamIndicatorCloseDisposable;
        private TweenTextScaleManager _textScaleManager = new TweenTextScaleManager();
        Data_Center _trackedRotationUnit;
        Data_Center _indicatorFocus;
        bool _showAllPlayerIndicators;
        readonly Vector3[] _playAreaCorners = new Vector3[4];
        readonly Vector3[] _portraitCorners = new Vector3[4];
        static readonly Vector3 StatusOffset = Vector3.up * 2.5f;
        static readonly Vector3 IndicatorOffset = Vector3.up * 1.5f;
        bool IsPlayerTeam => TeamConfig != null && TeamConfig.myTeam == RTFightManager.playerTeam;
        bool IsGroupBattle => FightLoad.Fight != null && FightLoad.Fight.IsGroupBattle;
        
        
        MultiDic<int, int, Data_Center> _teamMembers;
        public MultiDic<int, int, Data_Center> TeamMembers
        {
            get => _teamMembers;
            set => _teamMembers = value;
        }
        
        public void Clear()
        {
            _barPosUpdate?.Dispose();
            _teamIndicatorCloseDisposable?.Dispose();
            switch (TeamMode)
            {
                case TeamMode.MultiRaid:
                    MultiClear();
                    break;
                case TeamMode.Rotation:
                    RotateClear();
                    break;
            }
            _textScaleManager.Clear();
            if (IsPlayerTeam) HideSelectedFrame();
        }
        
        public void InsTeamUI(Action<Data_Center> changeUnit, Func<bool> currentAutoState, Action<bool> switchTeamAuto, ReactiveProperty<Data_Center> rModeUnit)
        {
            teamAutoSwitch.Initialize(currentAutoState, switchTeamAuto);
            if (TeamConfig.myTeam != RTFightManager.playerTeam)
            {
                teamAutoSwitch.gameObject.SetActive((CommonSetting.DevMode || FightLoad.Fight.EventType == FightEventType.Self)
                                                    && !IsGroupBattle);
            }
            else
            {
                teamAutoSwitch.gameObject.SetActive(!IsGroupBattle);
            }
            switch (TeamMode)
            {
                case TeamMode.MultiRaid:
                    InsTeamUI_Multi(switchTeamAuto, currentAutoState);
                    break;
                case TeamMode.Rotation:
                    IniTeamUI_Rotate(changeUnit);
                    IniComboHit(rModeUnit);
                    rModeUnit?.Subscribe(Refresh).AddTo(gameObject);
                    break;
            }
            RefreshHUDPresentation();
            StartStatusTracking();
        }
        
        void RefreshResistanceBar(Data_Center dataCenter, int value)
        {
            UnitIconDic.TryGetValue(dataCenter, out var tempSi);
            tempSi?.RefreshResistanceBar(value);
        }
        void RefreshHPBar(Data_Center dataCenter, float currentHp, float wholeHP)
        {
            UnitIconDic.TryGetValue(dataCenter, out var tempSi);
            tempSi?.RefreshHpBar(currentHp, wholeHP);
        }
        void RefreshExBar(Data_Center dataCenter, int currentEx)
        {
            UnitIconDic.TryGetValue(dataCenter, out var tempSi);
            tempSi?.RefreshExBar(currentEx);
        }
        
        void RefreshSuperComboFlg(Data_Center dataCenter, bool on)
        {
            UnitIconDic.TryGetValue(dataCenter, out var tempSi);
            tempSi?.DreamComboFlg.SetActive(on && !IsGroupBattle);
        }
        
        public void Refresh(Data_Center fighting = null)
        {
            if (_teamMembers?.mDict == null) return;
            _trackedRotationUnit = fighting;
            RefreshHUDPresentation();
            _teamIndicatorCloseDisposable?.Dispose();

            if (IsPlayerTeam && !IsGroupBattle)
            {
                if (TeamMode == TeamMode.Rotation)
                {
                    SetPlayerIndicators(fighting, false, true);
                    UpdateSelectedFrame(fighting);
                }
                else
                {
                    var focus = inputsManager.CurrentFocus.Value;
                    SetPlayerIndicators(focus, focus == null, focus == null);
                }
            }
            else
            {
                _indicatorFocus = null;
                _showAllPlayerIndicators = false;
            }
            StartStatusTracking();
        }

        // A layout resize can reapply geometry without restarting focus or the
        // temporary player marker timer.
        public void RefreshHUDPresentation()
        {
            if (IsPlayerTeam && sideIconsContainer != null)
                sideIconsContainer.gameObject.SetActive(!IsGroupBattle);
            foreach (var pair in UnitIconDic)
            {
                if (pair.Key != null && pair.Value != null)
                    ArrangeSideIcon(pair.Key, pair.Value);
            }
            EnsureStatusWidgetsHierarchy();
            UpdateTrackedWidgets();
        }

        void ArrangeSideIcon(Data_Center center, SideUnitIcon icon)
        {
            bool floating = IsGroupBattle || !IsPlayerTeam;
            var parent = floating ? _targetCanvasT : sideIconsContainer;
            if (parent != null && icon.transform.parent != parent)
                icon.transform.SetParent(parent, false);
            icon.transform.localScale = Vector3.one;
            icon.Icon.gameObject.SetActive(!floating);
            if (!floating) icon.RecallBars();
            icon.ApplyBattleHUDStyle(floating, IsGroupBattle);
            icon.SetBattleHUDTeamColor(IsPlayerTeam);

            // World markers must not remain descendants of a portrait in the
            // grid: moving such a child to a screen point drags it through HUD
            // layout space and leaves status bars piled up beside the rail.
            if (icon.TeamIndicator != null)
            {
                var marker = icon.TeamIndicator.rectTransform;
                if (_targetCanvasT != null && marker.parent != _targetCanvasT)
                    marker.SetParent(_targetCanvasT, false);
                marker.localScale = Vector3.one;
                marker.gameObject.SetActive(false);
            }
            if (center.FightDataRef.IsDead.Value) icon.GreyOut();
            if (floating)
                icon.gameObject.SetActive(ShouldShowFloatingStatus(center));
            else
                icon.gameObject.SetActive(true);
        }

        bool ShouldShowFloatingStatus(Data_Center center)
        {
            return center != null && !center.FightDataRef.IsDead.Value &&
                (IsGroupBattle || TeamMode != TeamMode.Rotation || center == _trackedRotationUnit);
        }

        void StartStatusTracking()
        {
            _barPosUpdate?.Dispose();
            UpdateTrackedWidgets();
            _barPosUpdate = Observable.IntervalFrame(Mathf.Max(1, barPosUpdateInterval))
                .Subscribe(_ => UpdateTrackedWidgets()).AddTo(gameObject);
        }

        void SetPlayerIndicators(Data_Center focus, bool showAll, bool temporary)
        {
            _teamIndicatorCloseDisposable?.Dispose();
            _indicatorFocus = focus;
            _showAllPlayerIndicators = showAll;
            if (temporary)
            {
                _teamIndicatorCloseDisposable = Observable.Timer(TimeSpan.FromSeconds(teamIndicatorCloseDelay))
                    .Subscribe(_ =>
                    {
                        _indicatorFocus = null;
                        _showAllPlayerIndicators = false;
                        UpdateTrackedWidgets();
                    }).AddTo(gameObject);
            }
            UpdateTrackedWidgets();
        }

        void UpdateTrackedWidgets()
        {
            foreach (var pair in UnitIconDic)
            {
                var center = pair.Key;
                var icon = pair.Value;
                if (center == null || icon == null) continue;
                bool floating = IsGroupBattle || !IsPlayerTeam;
                if (floating)
                {
                    if (ShouldShowFloatingStatus(center))
                        UpdateTrackedUiPosition(icon.transform, center.transform.position + StatusOffset);
                    else if (icon.gameObject.activeSelf)
                        icon.gameObject.SetActive(false);
                }

                var marker = icon.TeamIndicator;
                if (marker == null) continue;
                bool showMarker = IsPlayerTeam && !IsGroupBattle && !center.FightDataRef.IsDead.Value &&
                    (_showAllPlayerIndicators || center == _indicatorFocus);
                if (showMarker)
                    UpdateTrackedUiPosition(marker.transform, center.transform.position + IndicatorOffset);
                else if (marker.gameObject.activeSelf)
                    marker.gameObject.SetActive(false);
            }
        }

        void UpdateTrackedUiPosition(Transform target, Vector3 worldPosition)
        {
            var camera = CameraManager._camera;
            if (target == null || camera == null) return;
            var viewport = camera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)
            {
                if (target.gameObject.activeSelf) target.gameObject.SetActive(false);
                return;
            }
            if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);
            var screenPoint = camera.WorldToScreenPoint(worldPosition);
            var rect = target as RectTransform;
            var layer = _targetCanvasT != null ? _targetCanvasT.GetComponent<FightingStepLayer>() : null;
            var playArea = layer != null ? layer.MiddleArea : null;
            if (rect != null && playArea != null)
            {
                playArea.GetWorldCorners(_playAreaCorners);
                float halfWidth = rect.rect.width * Mathf.Abs(rect.lossyScale.x) * 0.5f;
                float halfHeight = rect.rect.height * Mathf.Abs(rect.lossyScale.y) * 0.5f;
                float left = _playAreaCorners[0].x + halfWidth;
                float right = Mathf.Max(left, _playAreaCorners[2].x - halfWidth);
                float bottom = _playAreaCorners[0].y + halfHeight;
                float top = Mathf.Max(bottom, _playAreaCorners[2].y - halfHeight);
                screenPoint.y = Mathf.Clamp(screenPoint.y, bottom, top);

                // Keep world status readable when a unit passes behind the
                // fixed portrait rail, without moving the rail itself.
                if (!IsGroupBattle && sideIconsContainer != null && sideIconsContainer.gameObject.activeInHierarchy)
                {
                    sideIconsContainer.GetWorldCorners(_portraitCorners);
                    if (screenPoint.y + halfHeight > _portraitCorners[0].y &&
                        screenPoint.y - halfHeight < _portraitCorners[2].y)
                    {
                        float railRight = _portraitCorners[2].x + halfWidth + 8 * Mathf.Abs(rect.lossyScale.x);
                        left = Mathf.Min(right, Mathf.Max(left, railRight));
                    }
                }
                screenPoint.x = Mathf.Clamp(screenPoint.x, left, right);
            }
            target.position = new Vector3(screenPoint.x, screenPoint.y, 0);
        }

        void UpdateSelectedFrame(Data_Center center)
        {
            if (!IsPlayerTeam || selectedFrame == null) return;
            if (!IsGroupBattle && center != null && UnitIconDic.TryGetValue(center, out var icon) && icon != null)
                HeroIcon.SelectedFeature(icon.Icon.transform, selectedFrame.gameObject, 1f);
            else
                HideSelectedFrame();
        }

        void HideSelectedFrame()
        {
            if (selectedFrame == null) return;
            selectedFrame.SetParent(transform, false);
            selectedFrame.gameObject.SetActive(false);
        }

        void EnsureStatusWidgetsHierarchy()
        {
            if (_targetCanvasT == null) return;
            int index = MoveStatusWidget(sideIconsContainer, 0);
            foreach (var icon in UnitIconDic.Values)
            {
                if (icon == null) continue;
                index = MoveStatusWidget(icon.transform, index);
                if (icon.TeamIndicator != null)
                    index = MoveStatusWidget(icon.TeamIndicator.transform, index);
            }
            if (rotationModeHitCombo != null) MoveStatusWidget(rotationModeHitCombo.transform, index);
        }

        int MoveStatusWidget(Transform widget, int index)
        {
            if (widget == null || widget.parent != _targetCanvasT) return index;
            widget.SetSiblingIndex(Mathf.Clamp(index, 0, _targetCanvasT.childCount - 1));
            return index + 1;
        }

        public SideUnitIcon GetSideIcon(Data_Center d)
        {
            return UnitIconDic[d];
        }
    }
}

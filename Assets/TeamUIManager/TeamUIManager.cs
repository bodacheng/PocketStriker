using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using DG.Tweening;

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
        public TeamMode TeamMode { get; set; }
        public TeamConfig TeamConfig { get; set; }
        public readonly IDictionary<Data_Center, SideUnitIcon> UnitIconDic = new Dictionary<Data_Center, SideUnitIcon>();
        private IDisposable _barPosUpdate;
        private IDisposable _teamIndicatorCloseDisposable;
        private TweenTextScaleManager _textScaleManager = new TweenTextScaleManager();
        
        
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
        }
        
        public void InsTeamUI(Action<Data_Center> changeUnit, Func<bool> currentAutoState, Action<bool> switchTeamAuto, ReactiveProperty<Data_Center> rModeUnit)
        {
            teamAutoSwitch.Initialize(currentAutoState, switchTeamAuto);
            if (TeamConfig.myTeam != RTFightManager.playerTeam)
            {
                teamAutoSwitch.gameObject.SetActive((CommonSetting.DevMode || FightLoad.Fight.EventType == FightEventType.Self)
                                                    && FightLoad.Fight.EventType != FightEventType.Gangbang);
            }
            else
            {
                teamAutoSwitch.gameObject.SetActive(FightLoad.Fight.EventType != FightEventType.Gangbang);
            }
            switch (TeamMode)
            {
                case TeamMode.MultiRaid:
                    InsTeamUI_Multi(switchTeamAuto, currentAutoState);
                    break;
                case TeamMode.Rotation:
                    IniTeamUI_Rotate(changeUnit);
                    IniComboHit(rModeUnit);
                    rModeUnit.Subscribe(Refresh).AddTo(gameObject);
                    break;
            }
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
            tempSi?.DreamComboFlg.SetActive(on);
        }
        
        public void Refresh(Data_Center fighting = null)
        {
            foreach (var dataCenter in _teamMembers.GetValues())
            {
                UnitIconDic.TryGetValue(dataCenter, out var tempSi);
                if (tempSi == null)
                    continue;
                if (TeamConfig.myTeam == RTFightManager.playerTeam)
                {
                    tempSi.transform.localScale = Vector3.one;
                    tempSi.transform.SetParent(sideIconsContainer.transform);
                    tempSi.Icon.gameObject.SetActive(true);
                    tempSi.RecallBars();
                }
                else
                {
                    tempSi.gameObject.SetActive(true);
                    tempSi.Icon.gameObject.SetActive(false);
                    tempSi.transform.SetParent(_targetCanvasT.transform);
                }
            }
            
            _barPosUpdate?.Dispose();
            _teamIndicatorCloseDisposable?.Dispose();
            switch (TeamMode)
            {
                case TeamMode.Rotation:
                    foreach (var dataCenter in _teamMembers.GetValues())
                    {
                        UnitIconDic.TryGetValue(dataCenter, out var tempSi);
                        if (tempSi == null)
                            continue;
                        tempSi.TeamIndicator.gameObject.SetActive(false);
                    }
                    
                    if (TeamConfig.myTeam != RTFightManager.playerTeam)
                    {
                        _barPosUpdate = Observable.IntervalFrame(barPosUpdateInterval).Subscribe(_ =>
                            {
                                if (fighting == null)
                                    return;
                                UnitIconDic.TryGetValue(fighting, out var tempSi);
                                _textScaleManager.AddNew(tempSi.transform, tempSi.transform.DOMove(CameraManager._camera.WorldToScreenPoint(fighting.transform.position + Vector3.up * 2.5f), 0.5f));
                            }
                        ).AddTo(gameObject);
                    }
                    else
                    {
                        if (fighting != null)
                        {
                            UnitIconDic.TryGetValue(fighting, out var tempSi);
                            tempSi.TeamIndicator.gameObject.SetActive(true);
                        }
                        _barPosUpdate = Observable.IntervalFrame(barPosUpdateInterval).Subscribe(_ =>
                            {
                                if (fighting == null)
                                    return;
                                UnitIconDic.TryGetValue(fighting, out var tempSi);
                                _textScaleManager.AddNew(tempSi.TeamIndicator.transform, tempSi.TeamIndicator.transform.DOMove(CameraManager._camera.WorldToScreenPoint(fighting.transform.position + Vector3.up * 1.5f), 0.5f));
                            }
                        ).AddTo(gameObject);
                        
                        _teamIndicatorCloseDisposable = Observable.Timer(TimeSpan.FromSeconds(teamIndicatorCloseDelay)).Subscribe(_ =>
                        {
                            _barPosUpdate.Dispose();
                            if (fighting == null)
                                return;
                            UnitIconDic.TryGetValue(fighting, out var tempSi);
                            tempSi.TeamIndicator.gameObject.SetActive(false);
                            // Add your code here to execute after disposing barPosUpdate
                            _teamIndicatorCloseDisposable.Dispose();
                        }).AddTo(gameObject);
                    }
                    break;
                case TeamMode.MultiRaid:
                    if (TeamConfig.myTeam != RTFightManager.playerTeam)
                    {
                        foreach (var dataCenter in _teamMembers.GetValues())
                        {
                            UnitIconDic.TryGetValue(dataCenter, out var tempSi);
                            if (tempSi == null)
                                continue;
                            tempSi.TeamIndicator.gameObject.SetActive(false);
                        }
                        _barPosUpdate = Observable.IntervalFrame(barPosUpdateInterval).Subscribe(_ =>
                        {
                            foreach (var one in _teamMembers.GetValues())
                            {
                                UnitIconDic.TryGetValue(one, out var tempSi);
                                if (tempSi != null)
                                    _textScaleManager.AddNew(tempSi.transform, tempSi.transform.DOMove(CameraManager._camera.WorldToScreenPoint(one.transform.position + Vector3.up * 2.5f), 0.5f));
                            }
                        }).AddTo(gameObject);
                    }
                    else
                    {
                        foreach (var one in _teamMembers.GetValues())
                        {
                            UnitIconDic.TryGetValue(one, out var tempSi);
                            if (inputsManager.CurrentFocus.Value == null)
                                tempSi.TeamIndicator.gameObject.SetActive(true);
                            else
                                tempSi.TeamIndicator.gameObject.SetActive(one == inputsManager.CurrentFocus.Value);
                        }
                        
                        if (inputsManager.CurrentFocus.Value == null)
                        {
                            _barPosUpdate = Observable.IntervalFrame(barPosUpdateInterval).Subscribe(_ =>
                            {
                                foreach (var one in _teamMembers.GetValues())
                                {
                                    UnitIconDic.TryGetValue(one, out var tempSi);
                                    _textScaleManager.AddNew(tempSi.TeamIndicator.transform,tempSi.TeamIndicator.transform.DOMove(CameraManager._camera.WorldToScreenPoint(one.transform.position + Vector3.up * 1.5f), 0.5f));
                                }
                            }).AddTo(gameObject);
                        
                            _teamIndicatorCloseDisposable = Observable.Timer(TimeSpan.FromSeconds(teamIndicatorCloseDelay)).Subscribe(_ =>
                            {
                                _barPosUpdate.Dispose();
                                foreach (var one in _teamMembers.GetValues())
                                {
                                    UnitIconDic.TryGetValue(one, out var tempSi);
                                    tempSi.TeamIndicator.gameObject.SetActive(false);
                                }
                                _teamIndicatorCloseDisposable.Dispose();
                            }).AddTo(gameObject);
                        }
                        else
                        {
                            inputsManager.CurrentFocus.SetValueAndForceNotify(inputsManager.CurrentFocus.Value); // for refresh
                        }
                    }
                    break;
            }
            
            if (FightLoad.Fight.EventType == FightEventType.Gangbang)
            {
                sideIconsContainer.gameObject.SetActive(false);
            }
        }
        
        public SideUnitIcon GetSideIcon(Data_Center d)
        {
            return UnitIconDic[d];
        }
    }
}
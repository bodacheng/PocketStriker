using System;
using DG.Tweening;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

namespace FightScene
{
    public partial class TeamUIManager : MonoBehaviour
    {
        [SerializeField] Text liveUnitCount;
        public Text LiveUnitCount => liveUnitCount;
        
        void SetLiveUnitCount()
        {
            int liveCount = 0;
            foreach (var dc in _teamMembers.GetValues())
            {
                if (!dc.FightDataRef.IsDead.Value)
                {
                    liveCount++;
                }
            }
            liveUnitCount.text = (TeamConfig.myTeam == RTFightManager.playerTeam ? "Player:":"Enemy:") +
                liveCount +  "/" + _teamMembers.GetValues().Count;
        }
        
        void MultiClear()
        {
            UnitIconDic.Clear();
        }
        
        void InsTeamUI_Multi(Action<bool> switchTeamAuto, Func<bool> currentAutoState)//这个环节应该能够同时把HP bar也适配好。
        {
            foreach (var center in _teamMembers.GetValues())
            {
                // SideIcon整备
                void ClickUnitIcon(Data_Center c)
                {
                    if (c.FightDataRef.IsDead.Value)
                    {
                        return;
                    }
                    
                    if (TeamConfig.myTeam == RTFightManager.playerTeam)
                    {
                        if (inputsManager.CurrentFocus.Value == c)
                        {
                            inputsManager.FocusUnit(null);
                        }
                        else
                        {
                            inputsManager.FocusUnit(c);
                        }
                    }
                    switchTeamAuto(currentAutoState());
                }
                
                var sideIcon = Instantiate(unitIconPrefab);
                sideIcon.name = center.UnitInfo.r_id + "_icon";
                sideIcon.Icon.iconButton.onClick.RemoveAllListeners();
                sideIcon.Icon.iconButton.onClick.AddListener(() =>
                {
                    ClickUnitIcon(center);
                });
                var unitInfo = RTFightManager.Target.UnitInfoRef[center];
                sideIcon.Icon.ChangeIcon(unitInfo);
                sideIcon.gameObject.SetActive(true);
                sideIcon.Icon.CooldownCurtainUpdate(0);
                
                if (TeamConfig.myTeam == RTFightManager.playerTeam)
                {
                    sideIcon.transform.SetParent(sideIconsContainer.transform);
                    sideIcon.transform.localScale = Vector3.one;
                }
                else
                {
                    sideIcon.transform.SetParent(_targetCanvasT.transform);
                    sideIcon.transform.localScale = Vector3.one;
                }
                DicAdd<Data_Center, SideUnitIcon>.Add(UnitIconDic, center, sideIcon);
                
                var maxHp = center.FightDataRef.CurrentHp.Value;
                center.FightDataRef.CurrentHp.Subscribe(x =>
                {
                    RefreshHPBar(center, x, maxHp);
                }).AddTo(gameObject);
                
                center.FightDataRef.CriticalGauge.Subscribe(x =>
                {
                    RefreshExBar(center, x);
                }).AddTo(gameObject);
                
                center.FightDataRef.DreamComboGauge.Subscribe(x =>
                {
                    RefreshSuperComboFlg(center,center.FightDataRef.HasPlentyDreamGauge());
                }).AddTo(RTFightManager.Target.Disposables);
                
                center.FightDataRef.Resistance.Subscribe(x =>
                    {
                        RefreshResistanceBar(center, x);
                    }
                ).AddTo(gameObject);
                
                center.FightDataRef.IsDead.Subscribe(x =>
                    {
                        if (x)
                        {
                            center.FightDataRef.Resistance.Value = 0;
                            center.FightDataRef.CriticalGauge.Value = 0;
                            sideIcon.GreyOut();
                            SetLiveUnitCount();
                        }
                    }
                ).AddTo(sideIcon.gameObject);
            }
            
            inputsManager.CurrentFocus.Subscribe(
                (x) =>
                {
                    if (x != null)
                    {
                        UnitIconDic.TryGetValue(x, out var targetIcon);
                        if (targetIcon != null)
                        {
                            selectedFrame.SetParent(targetIcon.Icon.transform);
                            selectedFrame.transform.localPosition = Vector3.zero;
                            selectedFrame.transform.localScale = Vector3.one;
                            selectedFrame.gameObject.SetActive(true);
                            selectedFrame.SetAsFirstSibling();
                        }
                    }
                    else
                    {
                        selectedFrame.SetParent(transform);
                        selectedFrame.gameObject.SetActive(false);
                    }
                    
                    if (TeamConfig.myTeam == RTFightManager.playerTeam)
                    {
                        RTFightManager.Target.CameraAdjustment(Team.player1, TeamMode.MultiRaid, FightLoad.Fight.EventType,x != null ? x.geometryCenter : null);
                        _teamIndicatorCloseDisposable?.Dispose();
                        _barPosUpdate?.Dispose();
                        foreach (var one in _teamMembers.GetValues())
                        {
                            UnitIconDic.TryGetValue(one, out var tempSi);
                            tempSi.TeamIndicator.gameObject.SetActive(inputsManager.CurrentFocus.Value == one);
                            if (x == one)
                            {
                                _barPosUpdate = Observable.IntervalFrame(barPosUpdateInterval).Subscribe(_ =>
                                {
                                    UnitIconDic.TryGetValue(one, out var tempSi);
                                    _textScaleManager.AddNew(tempSi.TeamIndicator.transform,
                                        tempSi.TeamIndicator.transform.DOMove(
                                            CameraManager._camera.WorldToScreenPoint(one.transform.position + Vector3.up * 1.5f), 0.5f)
                                    );
                                }).AddTo(gameObject);
                            }
                        }
                    }
                }
            ).AddTo(this.gameObject);
            
            inputsManager.FocusUnit(null);
            SetLiveUnitCount();
        }
    }
}
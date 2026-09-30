using System;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

namespace FightScene
{
    public partial class TeamUIManager : MonoBehaviour
    {
        [SerializeField] Text liveUnitCount;
        public Text LiveUnitCount => liveUnitCount;

        private int _liveUnitCountNum;
        public int LiveUnitCountNum => _liveUnitCountNum;

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
            _liveUnitCountNum = liveCount;
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
                    if (c.FightDataRef.IsDead.Value || IsGroupBattle)
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

                ArrangeSideIcon(center, sideIcon);

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

            inputsManager.CurrentFocus.Subscribe(x =>
            {
                // Both managers share the selection frame. Only the player
                // manager may attach it to a portrait or change camera focus.
                if (!IsPlayerTeam) return;
                UpdateSelectedFrame(x);
                if (!IsGroupBattle)
                {
                    RTFightManager.Target.CameraAdjustment(Team.player1, TeamMode.MultiRaid,
                        FightLoad.Fight.EventType, x != null ? x.geometryCenter : null);
                    SetPlayerIndicators(x, false, false);
                }
                else
                {
                    SetPlayerIndicators(null, false, false);
                }
                StartStatusTracking();
            }).AddTo(gameObject);

            if (IsPlayerTeam)
            {
                if (!IsGroupBattle)
                    inputsManager.FocusUnit(ResolveDefaultFocus(), true);
                switchTeamAuto(currentAutoState());
            }
            SetLiveUnitCount();
        }

        Data_Center ResolveDefaultFocus()
        {
            var currentFocus = inputsManager.CurrentFocus.Value;
            if (currentFocus != null && !currentFocus.FightDataRef.IsDead.Value)
            {
                return currentFocus;
            }

            foreach (var center in _teamMembers.GetValues())
            {
                if (center != null && !center.FightDataRef.IsDead.Value)
                {
                    return center;
                }
            }

            return null;
        }
    }
}

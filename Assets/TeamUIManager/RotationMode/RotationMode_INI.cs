using UnityEngine;
using UniRx;
using System;
using DG.Tweening;

namespace FightScene
{
    public partial class TeamUIManager : MonoBehaviour
    {
        void IniTeamUI_Rotate(Action<Data_Center> ChangeUnit)
        {
            foreach (var center in _teamMembers.GetValues())
            {
                var sideIcon = Instantiate(unitIconPrefab);
                sideIcon.name = center.name + " ICon";
                sideIcon.Icon.iconButton.onClick.RemoveAllListeners();
                sideIcon.Icon.iconButton.onClick.AddListener(() => { ChangeUnit(center); });
                var info = RTFightManager.Target.UnitInfoRef[center];
                sideIcon.Icon.ChangeIcon(info);
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
                
                RTFightManager.Target.RefreshTimeDic[center].Subscribe((x) =>
                {
                    UnitIconDic[center].Icon.CooldownCurtainUpdate(x/10);
                }).AddTo(RTFightManager.Target.Disposables);
                
                var maxHp = center.FightDataRef.CurrentHp.Value;
                center.FightDataRef.CurrentHp.Subscribe(x =>
                {
                    RefreshHPBar(center, x, maxHp);
                }).AddTo(RTFightManager.Target.Disposables);
                
                center.FightDataRef.CriticalGauge.Subscribe(x =>
                {
                    RefreshExBar(center, x);
                }).AddTo(RTFightManager.Target.Disposables);
                
                center.FightDataRef.DreamComboGauge.Subscribe(x =>
                {
                    RefreshSuperComboFlg(center,center.FightDataRef.HasPlentyDreamGauge());
                }).AddTo(RTFightManager.Target.Disposables);
                
                center.FightDataRef.Resistance.Subscribe(x =>
                {
                    RefreshResistanceBar(center, x);
                }).AddTo(RTFightManager.Target.Disposables);
                
                center.FightDataRef.IsDead.Subscribe(x => {
                    if (x)
                    {
                        center.FightDataRef.Resistance.Value = 0;
                        center.FightDataRef.CriticalGauge.Value = 0;
                        sideIcon.GreyOut();
                        // if (FightLogger.value.GetWinnerTeam() == Team.none)
                        // {
                        //     RTFightManager.Target.CameraAdjustment(RTFightManager.playerTeam, RTFightManager.Target.team1.TeamMode);
                        //     var c = RTFightManager.Target._CameraManager.GetMode(C_Mode.CertainYAntiVibration);
                        //     var mode = ((ChatGptFix)c);
                        //     mode.MePos = center.WholeT.position;
                        // }
                    }
                }).AddTo(sideIcon.gameObject);
            }
        }
        
        void RotateClear()
        {
            UnitIconDic.Clear();
            rotationModeHitCombo.text = "";
        }
        
        void IniComboHit(ReactiveProperty<Data_Center> RMode_Unit)
        {
            RMode_Unit.Subscribe(x =>
            {
                if (x != null)
                {
                    rotationModeHitCombo.name = TeamConfig.myTeam + "HitCombo";
                    rotationModeHitCombo.gameObject.SetActive(true);
                    if (rotationModeHitCombo.gameObject.transform.parent != _targetCanvasT)
                    {
                        rotationModeHitCombo.gameObject.transform.SetParent(_targetCanvasT.transform);
                    }
                    rotationModeHitCombo.transform.localScale = Vector3.one;
                    rotationModeHitCombo.fontSize = 30;
                    
                    x.FightDataRef._comboHitCount.HitCount.Subscribe(h =>
                    {
                        Vector2 GetComboTextShouldBePos(Vector3 unitWorldPos)
                        {
                            var mePos = CameraManager._camera.WorldToScreenPoint(unitWorldPos);
                            if (CameraManager._camera.WorldToViewportPoint(unitWorldPos).x < 0.5)
                            {
                                mePos = new Vector3(mePos.x / 2 , mePos.y, mePos.z);
                            }
                            else
                            {
                                mePos = new Vector3((mePos.x + Screen.width) / 2 , mePos.y, mePos.z);
                            }
                            return mePos;
                        }
                        
                        if (h > 1)
                        {
                            rotationModeHitCombo.text = h + ( h > 3 ? " Combo!!!": " Combo!" );
                            comboTextAnim.Play();
                            _textScaleManager.AddNew(
                                rotationModeHitCombo.transform,
                                rotationModeHitCombo.transform.DOMove(GetComboTextShouldBePos(x.transform.position), 
                                h == 2 ? 0 : 0.5f)
                            );
                        }
                        else
                        {
                            rotationModeHitCombo.text = null;
                        }
                    }).AddTo(RTFightManager.Target.Disposables);
                }
            }).AddTo(RTFightManager.Target.Disposables);
        }
    }
}

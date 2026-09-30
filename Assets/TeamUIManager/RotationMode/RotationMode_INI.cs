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
                sideIcon.name = center.UnitInfo.r_id + "_icon";
                sideIcon.Icon.iconButton.onClick.RemoveAllListeners();
                sideIcon.Icon.iconButton.onClick.AddListener(() =>
                {
                    if (!IsGroupBattle) ChangeUnit(center);
                });
                var info = RTFightManager.Target.UnitInfoRef[center];
                sideIcon.Icon.ChangeIcon(info);
                sideIcon.gameObject.SetActive(true);
                sideIcon.Icon.CooldownCurtainUpdate(0);
                
                ArrangeSideIcon(center, sideIcon);
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
            if (RMode_Unit == null) return;
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
                            // Floating combat feedback belongs to the play area, away
                            // from the header and the bottom touch controls.
                            var layer = _targetCanvasT.GetComponent<FightingStepLayer>();
                            var area = layer != null ? layer.MiddleArea : null;
                            if (area != null)
                            {
                                var corners = new Vector3[4];
                                area.GetWorldCorners(corners);
                                var textRect = rotationModeHitCombo.rectTransform;
                                float halfWidth = textRect.rect.width * textRect.lossyScale.x * 0.5f;
                                float halfHeight = textRect.rect.height * textRect.lossyScale.y * 0.5f;
                                float left = corners[0].x + halfWidth;
                                float right = Mathf.Max(left, corners[2].x - halfWidth);
                                float bottom = corners[0].y + halfHeight;
                                float top = Mathf.Max(bottom, corners[2].y - halfHeight);
                                mePos.x = Mathf.Clamp(mePos.x, left, right);
                                mePos.y = Mathf.Clamp(mePos.y, bottom, top);
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

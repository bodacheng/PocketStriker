using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using dataAccess;
using UnityEngine;
using UnityEngine.UI;
using mainMenu;
using ModelView;
using NoSuchStudio.Common;
using UniRx;

public class FrontLayer : UILayer
{
    [SerializeField] LowerBarIcon ArcadeBtn;
    [SerializeField] LowerBarIcon GangbangBtn;
    [SerializeField] LowerBarIcon ArenaBtn;
    [SerializeField] BOButton EventFightBtn;
    [SerializeField] BOButton TrainBtn;
    [SerializeField] Button SkillTestRBtn;
    [SerializeField] Button SkillTestMBtn;
    [SerializeField] Image view2D;
    [SerializeField] Animator unitOutAnimator;
    [SerializeField] DedicatedCameraConnector camConnector;
    [SerializeField] Button viewSwitchBtn;// 默认是非active
    [SerializeField] Text viewText;
    [SerializeField] float skillShowInterval = 5;

    public Action<bool> OnBusyStateChanged { get; set; }
    private readonly HashSet<string> _preparedModelRecordIds = new HashSet<string>();
    
    public void Initialise(PreScene pre)
    {
        var camRect = camConnector.GetComponent<RectTransform>();
        ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
        // CameraConnectorCal(view2D.GetComponent<RectTransform>(), cameraConnectorRightSpace, cameraConnectorVerticalSpace);
        // view2D.GetComponent<RectTransform>().anchoredPosition = camConnector.GetComponent<RectTransform>().anchoredPosition + new Vector2(camConnector.GetComponent<RectTransform>().sizeDelta.x / 2,0);
        
        ArcadeBtn.BOButton.onClick.AddListener(
        ()=>
            {
                ArcadeModeManager.Instance.DirectToArcadeStage(PlayerAccountInfo.Me.arcadeProcess + 1, true);
            });
        
        GangbangBtn.BOButton.onClick.AddListener(
            ()=>
            {
                if (PlayerAccountInfo.Me.arcadeProcess >= 5)
                    GangbangModeManager.Instance.DirectToGangStage(PlayerAccountInfo.Me.gangbangProcess + 1, true);
                else
                {
                    PopupLayer.ArrangeWarnWindow(Translate.Get("PlsClearStage5"));
                }
            });
        
        ArenaBtn.BOButton.onClick.AddListener(() =>
        {
            if (PlayerAccountInfo.Me.arcadeProcess >= 5)
                pre.trySwitchToStep(MainSceneStep.Arena);
            else
            {
                PopupLayer.ArrangeWarnWindow(Translate.Get("PlsClearStage5"));
            }
        });
        
        EventFightBtn.SetListener(() =>
        {
            if (PlayerAccountInfo.Me.arcadeProcess >= 5)
                pre.trySwitchToStep(MainSceneStep.EventFight);
            else
            {
                PopupLayer.ArrangeWarnWindow(Translate.Get("PlsClearStage5"));
            }
        });
        TrainBtn.onClick.AddListener(() => pre.trySwitchToStep(MainSceneStep.SelfFightFront));

        SkillTestRBtn.onClick.AddListener(pre.BeginSkillTest_Rotation);
        SkillTestMBtn.onClick.AddListener(pre.BeginSkillTest_Multi);
        SkillTestRBtn.gameObject.SetActive(CommonSetting.DevMode); 
        SkillTestMBtn.gameObject.SetActive(CommonSetting.DevMode);
        
        viewSwitchBtn.onClick.AddListener(ViewSwitch);
    }

    public void SetInteractive(bool on)
    {
        ArcadeBtn.BOButton.interactable = on;
        GangbangBtn.BOButton.interactable = on;
        ArenaBtn.BOButton.interactable = on;
        EventFightBtn.interactable = on;
        TrainBtn.interactable = on;
        SkillTestRBtn.interactable = on;
        SkillTestMBtn.interactable = on;
        viewSwitchBtn.interactable = on && viewSwitchBtn.gameObject.activeSelf;
    }

    void SetBusy(bool busy)
    {
        SetInteractive(!busy);
        OnBusyStateChanged?.Invoke(busy);
    }

    void StopRandomShowSkill()
    {
        _disposeShowSkill?.Dispose();
        _disposeShowSkill = null;
    }

    void Warmup3DModel(string recordID)
    {
        if (string.IsNullOrEmpty(recordID) || !_preparedModelRecordIds.Add(recordID))
        {
            return;
        }

        UniTask.Void(async () =>
        {
            try
            {
                await DedicatedCameraConnector.PrepareModel(recordID);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FrontLayer] Model warmup failed for {recordID}: {ex.Message}");
            }
        });
    }

    private IDisposable _disposeShowSkill;
    private List<string> skillList;
    void RegisterRandomShowSkill()
    {
        StopRandomShowSkill();
        if (skillList.Count > 3) // 3 是随便写的。反正就是身上只有一个被动技能的时候别运行的意思
        {
            _disposeShowSkill = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(skillShowInterval)).
                Subscribe((_) =>
                {
                    var skillId = skillList.Random();
                    camConnector.SkillShowRunWithPrepare(skillId).Forget();
                }).AddTo(gameObject);
        }
    }

    private bool view3D = true;
    private int _showMyModelRequestVersion;

    void ViewSwitch()
    {
        view3D = !view3D;
        ShowMyModel(instanceID).Forget();
    }
    
    private string instanceID;
    public async UniTask ShowMyModel(string instanceID)
    {
        int requestVersion = ++_showMyModelRequestVersion;
        SetBusy(true);
        ProgressLayer.Loading(string.Empty);
        try
        {
            this.instanceID = instanceID;
            var info = dataAccess.Units.Get(instanceID);
            if (info == null)
            {
                Debug.Log("error unit info:"+ instanceID);
                return;
            }

            var has2DView = AddressablesLogic.CheckKeyExist("unit_image", "unit_image/" + info.r_id);
            if (!has2DView)
            {
                view3D = true;
            }

            viewSwitchBtn.gameObject.SetActive(has2DView);
            viewText.text = view3D ? "3D" : "2D";
            if (view3D)
            {
                camConnector.gameObject.SetActive(true);
                view2D.gameObject.SetActive(false);

                if (camConnector.TaskRunningCount == 0)
                {
                    await camConnector.ShowModel(info?.r_id);
                    var equipments = Stones.GetEquippingStones(info?.id);
                    skillList = equipments.Select(x=>
                    {
                        var skillConfig =  SkillConfigTable.GetSkillConfigByRecordId(x.SkillId);
                        return skillConfig.REAL_NAME;
                    }).ToList();
                    if (this == null)
                    {
                        return;
                    }
                    RegisterRandomShowSkill();
                }
            }
            else
            {
                StopRandomShowSkill();
                camConnector.gameObject.SetActive(false);
                view2D.gameObject.SetActive(true);
                var sprite = await Set2DView(info.r_id, view2D, unitOutAnimator,
                    10, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtLeft(info.r_id));
                if (sprite == null)
                {
                    view3D = true;
                    ShowMyModel(instanceID).Forget();
                }
                else
                {
                    Warmup3DModel(info.r_id);
                }
            }
        }
        finally
        {
            if (requestVersion == _showMyModelRequestVersion)
            {
                ProgressLayer.Close();
                if (this != null)
                {
                    SetBusy(false);
                }
            }
        }
    }

    #region 教程
    public void PlsClickBtn(MainSceneStep btnCode)
    {
        ArcadeBtn.BOButton.interactable = btnCode == MainSceneStep.QuestInfo;
        ArenaBtn.BOButton.interactable = btnCode == MainSceneStep.Arena;
        GangbangBtn.BOButton.interactable = btnCode == MainSceneStep.GotchaFront;
        TrainBtn.interactable = btnCode == MainSceneStep.SelfFightFront;
        EventFightBtn.interactable = btnCode == MainSceneStep.EventFight;
        
        ArcadeBtn.Indicator.SetActive(false);
        ArenaBtn.Indicator.SetActive(false);
        GangbangBtn.Indicator.SetActive(false);
        
        Debug.Log("MainSceneStep:"+ btnCode);
        
        switch (btnCode)
        {
            case MainSceneStep.QuestInfo:
                ArcadeBtn.Indicator.SetActive(true);
                break;
            case MainSceneStep.Arena:
                ArenaBtn.Indicator.SetActive(true);
                break;
            case MainSceneStep.SelfFightFront:
                GangbangBtn.Indicator.SetActive(true);
                break;
        }
    }
    #endregion

    public override void OnDestroy()
    {
        StopRandomShowSkill();
        OnBusyStateChanged = null;
        base.OnDestroy();
    }
}

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
    [SerializeField] BOButton ArcadeBtn;
    [SerializeField] BOButton GangbangBtn;
    [SerializeField] BOButton ArenaBtn;
    [SerializeField] BOButton TrainBtn;
    [SerializeField] Button SkillTestRBtn;
    [SerializeField] Button SkillTestMBtn;
    [SerializeField] Image view2D;
    [SerializeField] Animator unitOutAnimator;
    [SerializeField] DedicatedCameraConnector camConnector;
    [SerializeField] Button viewSwitchBtn;// 默认是非active
    [SerializeField] Text viewText;
    [SerializeField] float skillShowInterval = 5;
    
    public void Initialise(PreScene pre)
    {
        var camRect = camConnector.GetComponent<RectTransform>();
        ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
        // CameraConnectorCal(view2D.GetComponent<RectTransform>(), cameraConnectorRightSpace, cameraConnectorVerticalSpace);
        // view2D.GetComponent<RectTransform>().anchoredPosition = camConnector.GetComponent<RectTransform>().anchoredPosition + new Vector2(camConnector.GetComponent<RectTransform>().sizeDelta.x / 2,0);
        
        ArcadeBtn.onClick.AddListener(
        ()=>
            {
                PlayerAccountInfo.Me.ArcadeModeManager.DirectToArcadeStage(PlayerAccountInfo.Me.arcadeProcess + 1, true);
            });
        
        GangbangBtn.onClick.AddListener(
            ()=>
            {
                PlayerAccountInfo.Me.GangbangModeManager.DirectToGangStage(PlayerAccountInfo.Me.gangbangProcess + 1, true);
            });
        GangbangBtn.gameObject.SetActive(PlayerAccountInfo.Me.arcadeProcess >= 5);
        
        ArenaBtn.onClick.AddListener(() =>
        {
            if (PlayerAccountInfo.Me.arcadeProcess >= 5)
                pre.trySwitchToStep(MainSceneStep.Arena);
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

    private IDisposable _disposeShowSkill;
    private List<string> skillList;
    void RegisterRandomShowSkill()
    {
        _disposeShowSkill?.Dispose();
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

    void ViewSwitch()
    {
        view3D = true; //!view3D;
        ShowMyModel(instanceID).Forget();
    }
    
    private string instanceID;
    public async UniTask ShowMyModel(string instanceID)
    {
        ProgressLayer.Loading(string.Empty);
        this.instanceID = instanceID;
        var info = dataAccess.Units.Get(instanceID);
        if (info == null)
        {
            Debug.Log("error unit info:"+ instanceID);
            return;
        }
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
            camConnector.gameObject.SetActive(false);
            view2D.gameObject.SetActive(true);
            var sprite = await Set2DView(info.r_id, view2D, unitOutAnimator, 
                10, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtLeft(info.r_id));
            if (sprite == null)
            {
                ViewSwitch();
            }
            else
            {
                viewSwitchBtn.gameObject.SetActive(true);
            }
        }
        ProgressLayer.Close();
    }

    #region 教程
    [SerializeField] private GameObject indicator;

    public void PlsClickBtn(string btnCode)
    {
        ArcadeBtn.interactable = btnCode == "arcade";
        ArenaBtn.interactable = btnCode == "arena";
        //MemberBtn.interactable = btnCode == "unit";
        TrainBtn.interactable = btnCode == "train";
        //StonesBtn.interactable = btnCode == "stones";
        //GotchaBtn.interactable = btnCode == "gotcha";

        Transform localPos = null;
        switch (btnCode)
        {
            case "arcade":
                localPos = ArcadeBtn.transform;
                break;
            case "arena":
                localPos = ArenaBtn.transform;
                break;
            case "unit":
                //localPos = MemberBtn.transform;
                break;
            case "train":
                localPos = TrainBtn.transform;
                break;
            case "stones":
                //localPos = StonesBtn.transform;
                break;
            case "gotcha":
                //localPos = GotchaBtn.transform;
                break;
        }

        indicator.transform.SetParent(localPos);
        indicator.transform.localPosition = Vector3.zero;
        indicator.SetActive(true);
    }
    #endregion
}

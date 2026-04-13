using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;
using FightScene;
using UnityEngine.Events;
using UnityEngine.UI;

public class FightingStepLayer : UILayer
{
    const int TopButtonSortingOrder = 1000;
    const float TeamMoveJoystickTouchWidth = 40f;
    const float TeamMoveJoystickTouchHeight = 35f;
    const float RotateCameraJoystickTouchWidth = 45f;
    const float RotateCameraJoystickTouchHeight = 35f;

    [Header("Pause Button")]
    [SerializeField] BOButton pauseButton;
    
    [Header("MobileInputsManager")]
    [SerializeField] MobileInputsManager inputsManager;
    
    [Header("TeamUIManager")]
    [SerializeField] TeamUIManager team1UI;
    [SerializeField] TeamUIManager team2UI;
    
    [Header("Tutorial")]
    [SerializeField] ClickNextTutorial clickNextTutorial;
    
    [Header("DreamCombo Tutorial")]
    [SerializeField] GameObject clickTriggerDreamCombo;

    [Header("教程强制点自动环节黑幕")] 
    [SerializeField] GameObject forceClickAutoBtnBlackMask;
    
    public TeamUIManager Team1UI => team1UI;
    public TeamUIManager Team2UI => team2UI;
    
    public MobileInputsManager InputsManager => inputsManager;
    
    public static FightingStepLayer Open()
    {
        var fightingLayer = UILayerLoader.Load<FightingStepLayer>();
        Debug.Log($"[FightingStepLayer] Open -> id={fightingLayer.GetInstanceID()} active={fightingLayer.gameObject.activeSelf} path={fightingLayer.transform.root.name}/{fightingLayer.transform.name}");
        fightingLayer.InputsManager.FXCamera = FightScene.FightScene.target.fxCamera;
        return fightingLayer;
    }
    
    public void PreparingMode(bool preparingMode)
    {
        if (team1UI.TeamMode == TeamMode.MultiRaid)
        {
            team1UI.Refresh();
        }
        if (team2UI.TeamMode == TeamMode.MultiRaid)
        {
            team2UI.Refresh();
        }
        inputsManager.PreparingMode(preparingMode);
        pauseButton.gameObject.SetActive(!preparingMode);
    }
    
    public async UniTask Setup(bool active = true)
    {
        gameObject.SetActive(active && FightLoad.Fight.EventType != FightEventType.Screensaver);
        await StartUp(
            (x) =>
            {
                PlayerPrefs.SetInt("auto", x ? 1:0);
                FightLoad.Fight.Team1Auto = x;
                RTFightManager.Target.team1.Auto = x;
            },
            (x) =>
            {
                FightLoad.Fight.Team2Auto = x;
                RTFightManager.Target.team2.Auto = x;
            },
            ()=>
            {
                var pauseLayer = UILayerLoader.Load<FightScenePauseSupport>();
                pauseLayer.Setup(
                    ()=> { Time.timeScale = 0; },
                    ()=>FightScene.FightScene.target.ReturnToFront(),
                    () =>
                    {
                        Time.timeScale = 1;
                        UILayerLoader.Remove<FightScenePauseSupport>();
                        AppSetting.Save();
                    }
                );
            });
    }
    
    public static void Close()
    {
        var layer = UILayerLoader.Get<FightingStepLayer>();
        if (layer == null)
            return;
        Debug.Log($"[FightingStepLayer] Close -> id={layer.GetInstanceID()}");
        layer.inputsManager.FocusUnit(null);
        layer.inputsManager.Clear();
        layer.team1UI.Clear();
        layer.team2UI.Clear();
        UILayerLoader.Remove<FightingStepLayer>();
    }

    public void OpenTutorial()
    {
        pauseButton.gameObject.SetActive(false);
        clickNextTutorial.Open();
    }

    public bool Initialized { get; set; } = false;

    async UniTask StartUp(Action<bool> switchTeam1Auto, Action<bool> switchTeam2Auto, Action pauseAction)
    {
        Initialized = false;
        ResetOverlayStates();
        
        RTFightManager.Target.team1.InputsManager = inputsManager;
        RTFightManager.Target.team2.InputsManager = inputsManager;
        
        pauseButton.SetListener(pauseAction.Invoke);
        
        team1UI.TeamMode = FightLoad.Fight.team1Mode;
        team2UI.TeamMode = FightLoad.Fight.team2Mode;
        team1UI.TeamConfig = RTFightManager.Target.heroTeamConfig;
        team2UI.TeamConfig = RTFightManager.Target.EnemyTeamConfig;
        team1UI.TeamConfig.playID = FightLoad.Fight.Team1ID;
        team2UI.TeamConfig.playID = FightLoad.Fight.Team2ID;
        team1UI.TeamMembers = RTFightManager.Target.team1.teamMembers;
        team2UI.TeamMembers = RTFightManager.Target.team2.teamMembers;
        
        // 角色第二次初始化在这之前已经结束
        team1UI.InsTeamUI(RTFightManager.Target.team1.ReadyForNextMember, (() => RTFightManager.Target.team1.Auto),switchTeam1Auto, RTFightManager.Target.team1.RMode_Unit);
        team2UI.InsTeamUI(RTFightManager.Target.team2.ReadyForNextMember, (() => RTFightManager.Target.team2.Auto),switchTeam2Auto, RTFightManager.Target.team2.RMode_Unit);

        team1UI.LiveUnitCount.gameObject.SetActive(FightLoad.Fight.EventType == FightEventType.Gangbang);
        team2UI.LiveUnitCount.gameObject.SetActive(FightLoad.Fight.EventType == FightEventType.Gangbang);
        
        var members = RTFightManager.Target.team1.teamMembers.GetValues();
        var inputEffectsLoading = new List<UniTask>();
        foreach (var d in members)
        {
            inputEffectsLoading.Add(inputsManager.ElementRegister(d.element, RTFightManager.Target.UnitInfoRef[d]));
        }

        await UniTask.WhenAll(inputEffectsLoading);
        inputsManager.GroupSkillIcons();
        KeepTopButtonsClickable();
        
        // foreach (var d in RTFightManager.Target.team2.teamMembers.GetValues())
        // {
        //     await inputsManager.ElementRegister(d.element, RTFightManager.Target.UnitInfoRef[d]);
        // }
        Initialized = true;
    }

    void ResetOverlayStates()
    {
        forceClickAutoBtnBlackMask.SetActive(false);
        clickTriggerDreamCombo.SetActive(false);
        clickNextTutorial.gameObject.SetActive(false);
    }

    void KeepTopButtonsClickable()
    {
        NormalizeJoystickTouchAreas();
        PromoteButtonToOverlayCanvas(pauseButton.transform, TopButtonSortingOrder);
        PromoteButtonToOverlayCanvas(team1UI.AutoSwitch.transform, TopButtonSortingOrder + 1);
        PromoteButtonToOverlayCanvas(team2UI.AutoSwitch.transform, TopButtonSortingOrder + 1);
        pauseButton.transform.parent.SetAsLastSibling();
        DisableRaycastTarget(transform.Find("top"));
        DisableRaycastTarget(transform.Find("middle"));
        DisableRaycastTarget(transform.Find("bottom"));
        team2UI.AutoSwitch.transform.SetAsLastSibling();
        team1UI.AutoSwitch.transform.SetAsLastSibling();
        pauseButton.transform.SetAsLastSibling();
    }

    void NormalizeJoystickTouchAreas()
    {
        foreach (var joystick in GetComponentsInChildren<UltimateJoystick>(true))
        {
            if (joystick == null)
            {
                continue;
            }

            switch (joystick.joystickName)
            {
                case "joystick":
                    UpdateJoystickTouchArea(joystick, TeamMoveJoystickTouchWidth, TeamMoveJoystickTouchHeight, 0f, 0f);
                    break;
                case "RotateCamera":
                    UpdateJoystickTouchArea(joystick, RotateCameraJoystickTouchWidth, RotateCameraJoystickTouchHeight, 0f, 100f);
                    break;
            }
        }
    }

    static void UpdateJoystickTouchArea(UltimateJoystick joystick, float width, float height, float vertical, float horizontal)
    {
        joystick.customActivationRange = true;
        joystick.activationWidth = width;
        joystick.activationHeight = height;
        joystick.activationPositionVertical = vertical;
        joystick.activationPositionHorizontal = horizontal;
        joystick.UpdatePositioning();
    }

    static void DisableRaycastTarget(Transform target)
    {
        var graphic = target != null ? target.GetComponent<Graphic>() : null;
        if (graphic != null)
        {
            graphic.raycastTarget = false;
        }
    }

    static void PromoteButtonToOverlayCanvas(Transform target, int sortingOrder)
    {
        if (target == null)
        {
            return;
        }

        var canvas = target.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = target.gameObject.AddComponent<Canvas>();
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (target.GetComponent<GraphicRaycaster>() == null)
        {
            target.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    public void ForceClickAutoBtn()
    {
        forceClickAutoBtnBlackMask.SetActive(true);
        UnityAction handler = null;
        handler = () =>
        {
            clickNextTutorial.Button.onClick.RemoveListener(handler);
            team1UI.AutoSwitch.ChangeAutoState(true);
            forceClickAutoBtnBlackMask.SetActive(false);
        };
        clickNextTutorial.Button.onClick.AddListener(handler);
    }

    bool preTeam1AIState;
    
    public void TutorialModeForceOnClickDreamCombo()
    {
        clickTriggerDreamCombo.SetActive(false);
        inputsManager.DreamComboDown();
        Team1UI.AutoSwitch.ChangeAutoState(preTeam1AIState);
        Team2UI.AutoSwitch.ChangeAutoState(true);
    }
    
    public void ForceClickDreamComboBtn()
    {
        preTeam1AIState = Team1UI.AutoSwitch.CurrentState();
        clickTriggerDreamCombo.SetActive(true);
    }

    private void OnDisable()
    {
        var c = RTFightManager.Target._CameraManager.GetMode(C_Mode.CertainYAntiVibration);
        var mode = ((ChatGptFix)c);
        mode.CanSetH = false;
    }

    public override void OnDestroy()
    {
        var c = RTFightManager.Target._CameraManager.GetMode(C_Mode.CertainYAntiVibration);
        var mode = ((ChatGptFix)c);
        mode.CanSetH = false;
        base.OnDestroy();
    }
}

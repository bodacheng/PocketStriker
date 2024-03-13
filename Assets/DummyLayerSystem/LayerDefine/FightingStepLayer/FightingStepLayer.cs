using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;
using FightScene;

public class FightingStepLayer : UILayer
{
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
                RTFightManager.Target.team1.Auto = x;
            },
            (x) =>
            {
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
                    }
                );
            });
    }
    
    public static void Close()
    {
        var layer = UILayerLoader.Get<FightingStepLayer>();
        if (layer == null)
            return;
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
        
        var inputEffectsLoading = new List<UniTask>();
        foreach (var d in RTFightManager.Target.team1.teamMembers.GetValues())
        {
            inputEffectsLoading.Add(inputsManager.ElementRegister(d.element, RTFightManager.Target.UnitInfoRef[d]));
        }
        await UniTask.WhenAll(inputEffectsLoading);
        inputsManager.GroupSkillIcons();
        
        // foreach (var d in RTFightManager.Target.team2.teamMembers.GetValues())
        // {
        //     await inputsManager.ElementRegister(d.element, RTFightManager.Target.UnitInfoRef[d]);
        // }
        Initialized = true;
    }

    public void ForceClickAutoBtn()
    {
        forceClickAutoBtnBlackMask.SetActive(true);
        clickNextTutorial.Button.onClick.AddListener(
            ()=>
            {
                team1UI.AutoSwitch.ChangeAutoState(true);
                forceClickAutoBtnBlackMask.SetActive(false);
            });
    }
    
    public void ForceClickDreamComboBtn(Action afterClick)
    {
        clickTriggerDreamCombo.SetActive(true);
        void Call()
        {
            clickTriggerDreamCombo.SetActive(false);
        }
        inputsManager.DreamComboBtn.onClick.AddListener(Call);
        inputsManager.DreamComboBtn.onClick.AddListener(() =>
        {
            inputsManager.DreamComboBtn.onClick.RemoveListener(Call);
            afterClick.Invoke();
        });
        inputsManager.DreamComboBtn.transform.SetAsLastSibling();
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

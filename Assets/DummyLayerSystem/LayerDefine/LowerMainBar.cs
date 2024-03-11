using System.Collections.Generic;
using DummyLayerSystem;
using UnityEngine;
using mainMenu;
using UniRx;

public class LowerMainBar : UILayer
{
    [SerializeField] private LowerBarIcon playTab;
    [SerializeField] private LowerBarIcon fighterTab;
    [SerializeField] private LowerBarIcon stoneTab;
    [SerializeField] private LowerBarIcon gotchaTab;
    [SerializeField] GameObject hasStoneToBeUpdateBadge;

    private List<LowerBarIcon> icons;
    
    private void Awake()
    {
        icons = new List<LowerBarIcon>()
        {
            playTab,fighterTab,stoneTab,gotchaTab
        };
    }

    void SelectUI(MainSceneStep step)
    {
        LowerBarIcon targetBtn = null;
        switch (step)
        {
            case MainSceneStep.FrontPage:
            case MainSceneStep.Arena:
            case MainSceneStep.Ranking:
            case MainSceneStep.ArenaAward:
            case MainSceneStep.ArcadeFront:
            case MainSceneStep.QuestInfo:
            case MainSceneStep.GangBangFront:
            case MainSceneStep.SelfFightFront:
                targetBtn = playTab;
                break;
            case MainSceneStep.UnitList:
            case MainSceneStep.UnitSkillEdit:
                targetBtn = fighterTab;
                break;
            case MainSceneStep.SkillStoneList:
            case MainSceneStep.SkillStones_Sell:
                targetBtn = stoneTab;
                break;
            case MainSceneStep.GotchaFront:
            case MainSceneStep.GotchaResult:
            case MainSceneStep.DropTableInfo:
                targetBtn = gotchaTab;
                break;
        }

        foreach (var icon in icons)
        {
            icon.Animator.SetBool("selected", icon == targetBtn);
        }
    }
    
    void Initialise(PreScene pre)
    {
        ProcessesRunner.Main.CurrentStep.Subscribe(SelectUI).AddTo(this.gameObject);
        
        void Go(MainSceneStep step)
        {
            UILayerLoader.Remove<ReturnLayer>();
            ReturnLayer.ReturnMissionList.Clear();
            pre.trySwitchToStep(step, false);
        }
        
        playTab.BOButton.SetListener(() =>
        {
            Go(MainSceneStep.FrontPage);
        });
        fighterTab.BOButton.SetListener(() =>
        {
            Go(MainSceneStep.UnitList);
        });
        stoneTab.BOButton.SetListener(() =>
        {
            Go(MainSceneStep.SkillStoneList);
        });
        gotchaTab.BOButton.SetListener(() =>
        {
            Go(MainSceneStep.GotchaFront);
        });
        
        hasStoneToBeUpdateBadge.SetActive(SSLevelUpManager.HasStoneToBeUpdate());
    }

    public static void Open()
    {
        var lowerMainBar = UILayerLoader.Load<LowerMainBar>();
        lowerMainBar.Initialise(PreScene.target);
        lowerMainBar.transform.SetAsLastSibling();
    }
}

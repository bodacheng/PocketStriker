using System;
using System.Collections.Generic;
using DummyLayerSystem;
using UnityEngine;
using mainMenu;

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
                targetBtn = playTab;
                break;
            case MainSceneStep.UnitList:
                targetBtn = fighterTab;
                break;
            case MainSceneStep.SkillStoneList:
                targetBtn = stoneTab;
                break;
            case MainSceneStep.GotchaFront:
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
        void Go(MainSceneStep step)
        {
            SelectUI(step);
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

    public static void Open(MainSceneStep step)
    {
        var lowerMainBar = UILayerLoader.Load<LowerMainBar>();
        lowerMainBar.Initialise(PreScene.target);
        lowerMainBar.transform.SetAsLastSibling();
        lowerMainBar.SelectUI(step);
    }
}

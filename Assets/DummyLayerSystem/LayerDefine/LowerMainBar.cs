using DummyLayerSystem;
using UnityEngine;
using mainMenu;

public class LowerMainBar : UILayer
{
    [SerializeField] private BOButton playTab;
    [SerializeField] private BOButton fighterTab;
    [SerializeField] private BOButton stoneTab;
    [SerializeField] private BOButton gotchaTab;
    [SerializeField] GameObject hasStoneToBeUpdateBadge;
    
    void Initialise(PreScene pre)
    {
        playTab.SetListener(() =>
        {
            ReturnLayer.ReturnMissionList.Clear();
            pre.trySwitchToStep(MainSceneStep.FrontPage, false);
        });
        fighterTab.SetListener(() =>
        {
            ReturnLayer.ReturnMissionList.Clear();
            pre.trySwitchToStep(MainSceneStep.UnitList, false);
        });
        stoneTab.SetListener(() =>
        {
            ReturnLayer.ReturnMissionList.Clear();
            pre.trySwitchToStep(MainSceneStep.SkillStoneList, false);
        });
        gotchaTab.SetListener(() =>
        {
            ReturnLayer.ReturnMissionList.Clear();
            pre.trySwitchToStep(MainSceneStep.GotchaFront, false);
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

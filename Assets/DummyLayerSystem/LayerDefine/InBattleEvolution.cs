using System;
using FightScene;
using UnityEngine;
using UnityEngine.UI;

public class InBattleEvolution : UILayer
{
    [SerializeField] private NineForShow nineForShow;
    [SerializeField] private Text upperText;
    [SerializeField] private Text bottomText;
    [SerializeField] private EvolutionSkill[] skillOptions;
    [SerializeField] private GameObject selectedFrame;

    public async void Setup(Data_Center focusUnit, Action onFinishedSkillEvolution, string upperText, string bottomText)
    {
        var set = focusUnit.UnitInfo.set;
        await nineForShow.ShowStones(
            set.a1, set.a2, set.a3,
            set.b1, set.b2, set.b3,
            set.c1, set.c2, set.c3
        );
        nineForShow.AddOnClickToSlots(
            (BOButton btn) =>
            {
                selectedFrame.SetActive(true);
                selectedFrame.transform.SetParent(btn.GetComponent<RectTransform>());
                selectedFrame.transform.localPosition = Vector3.zero;
                selectedFrame.GetComponent<RectTransform>().localPosition = new Vector3(0, 0, 0);
                selectedFrame.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
                selectedFrame.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
                selectedFrame.gameObject.SetActive(true);
            }
        );
        ShowSkillsToChoose(focusUnit, onFinishedSkillEvolution);
        
        this.upperText.text = upperText;
        this.bottomText.text = bottomText;
    }
    
    void ShowSkillsToChoose(Data_Center focusUnit, Action onFinishedSkillEvolution)
    {
        var recommendedTargetReplaceSlot = 
            focusUnit.UnitInfo.set.RecommendedTargetReplaceSlot
                (RTFightManager.Target.EvolutionManager.EvolutionCount >= 3);
        if (RTFightManager.Target.EvolutionManager.EvolutionCount < 3)
            nineForShow.EvolutionModeSlotInteractiveRefresh(focusUnit.UnitInfo.set);
        nineForShow.ClickTargetSlot(recommendedTargetReplaceSlot);
        
        var skills = RTFightManager.Target.EvolutionManager.RandomSkillList("human", focusUnit.UnitInfo.set);
        for (var i = 0; i < skillOptions.Length; i++)
        {
            var index = i;
            skillOptions[i].Btn.SetListener(
                async () =>
                {
                    await RTFightManager.Target.EvolutionManager.ChangeSkill(focusUnit, nineForShow.ClickedSlot, skills[index]);
                    onFinishedSkillEvolution.Invoke();
                });
            
            skillOptions[i].ShowIcon(skills[i]);
        }
    }
}

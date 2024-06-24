using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
    [SerializeField] private float animEndInSeconds = 0.5f;
    public async void Setup(Data_Center focusUnit, Action onFinishedSkillEvolution, string upperText, string bottomText)
    {
        var set = focusUnit.UnitInfo.set;

        await UniTask.WhenAll(
            nineForShow.ShowStones(
                set.a1, set.a2, set.a3,
                set.b1, set.b2, set.b3,
                set.c1, set.c2, set.c3
            ),
            ShowSkillsToChoose(focusUnit, onFinishedSkillEvolution)
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
        
        var recommendedTargetReplaceSlot = 
            focusUnit.UnitInfo.set.RecommendedTargetReplaceSlot
                (RTFightManager.Target.EvolutionManager.EvolutionCount >= 3);
        
        nineForShow.ClickTargetSlot(recommendedTargetReplaceSlot);
        
        this.upperText.text = upperText;
        this.bottomText.text = bottomText;
    }
    
    async UniTask ShowSkillsToChoose(Data_Center focusUnit, Action onFinishedSkillEvolution)
    {
        await nineForShow.EvolutionModeSlotInteractiveRefresh(focusUnit.UnitInfo.set, RTFightManager.Target.EvolutionManager.EvolutionCount >= 3);
        var skills = RTFightManager.Target.EvolutionManager.RandomSkillList("human", focusUnit.UnitInfo.set);
        for (var i = 0; i < skillOptions.Length; i++)
        {
            var index = i;
            skillOptions[i].Btn.SetListener(
                async () =>
                {
                    await RTFightManager.Target.EvolutionManager.ChangeSkill(focusUnit, nineForShow.ClickedSlot, skills[index]);
                    var t = skillOptions[index].Btn.transform.GetComponentInChildren<SKStoneItem>();
                    t.transform.DOMove( nineForShow.GetClickedSlot().transform.position, animEndInSeconds);
                    for (var a = 0; a < skillOptions.Length; a++)
                    {
                        if (a != index)
                        {
                            skillOptions[a].Animator.SetTrigger("fade");
                        }
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(animEndInSeconds));
                    onFinishedSkillEvolution.Invoke();
                });
            skillOptions[i].ShowIcon(skills[i]);
        }
    }
}

using System;
using UnityEngine;

public class EventBattleButton : MonoBehaviour
{
    [SerializeField] private BOButton btn;
    [SerializeField] private RewardUI rewardUI;
    [SerializeField] private GameObject clearedBadge;
    [SerializeField] private Animator animator;

    public void Setup(Action feature, Award award, bool cleared)
    {
        btn.SetListener(feature);
        rewardUI.ShowRewards(award.d, award.g);
        rewardUI.AwardRender(cleared);
        clearedBadge.SetActive(cleared);
        this.gameObject.SetActive(true);
        animator.SetBool("cleared", cleared);
    }
}

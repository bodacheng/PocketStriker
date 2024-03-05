using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

public class NullStateProcess : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AnimatorTransitionInfo _AnimatorTransitionInfo = animator.GetAnimatorTransitionInfo(layerIndex);

        if (!_AnimatorTransitionInfo.IsName("Full Body.null -> Full Body.full_body_state1")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.null -> Full Body.full_body_state2")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.full_body_state2 -> Full Body.null")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.full_body_state1 -> Full Body.null")
           )
        {
            animator.SetLayerWeight(layerIndex, 0);
        }
        else {
            animator.SetLayerWeight(layerIndex, 1);
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetLayerWeight(layerIndex, 1);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AnimatorTransitionInfo _AnimatorTransitionInfo = animator.GetAnimatorTransitionInfo(layerIndex);

        if (!_AnimatorTransitionInfo.IsName("Full Body.null -> Full Body.full_body_state1")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.null -> Full Body.full_body_state2")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.full_body_state2 -> Full Body.null")
            &&
            !_AnimatorTransitionInfo.IsName("Full Body.full_body_state1 -> Full Body.null")
           )
        {
            animator.SetLayerWeight(layerIndex, 0);
        }else{
            animator.SetLayerWeight(layerIndex, 1);
        }
    }
}
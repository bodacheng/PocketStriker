using UnityEngine;

public class StateEnterd : StateMachineBehaviour
{    
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //if (stateInfo.IsName("Full Body.null"))
        //    Debug.Log("null state");
        //if (stateInfo.IsName("Full Body.full_body_state1"))
        //    Debug.Log("full_body_state1");
        //if (stateInfo.IsName("Full Body.full_body_state2"))
        //    Debug.Log("full_body_state2");
        //if (stateInfo.IsName("Full Body.full_body_state3"))
            //Debug.Log("full_body_state3");
        animator.SetBool("in_transition",false);
    }
}
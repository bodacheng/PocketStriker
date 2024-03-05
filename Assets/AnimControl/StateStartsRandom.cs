using UnityEngine;

public class StateStartsRandom : StateMachineBehaviour
{
    
    bool start = true;
    float offset = 0.0f;

    void OnEnable()
    {
       
    }

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        offset = Random.Range(0f, 1f);
        if (start) {  
			animator.Play(stateInfo.fullPathHash, -1, offset);
            start = false;
        }
    }
}
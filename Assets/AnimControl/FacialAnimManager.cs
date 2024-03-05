using System;
using UniRx;
using UnityEngine;

public class FacialAnimManager : MonoBehaviour
{
    [SerializeField] AnimationClip defaultAnimation;
    [SerializeField] AnimationClip winkAnimation;
    [SerializeField] AnimationClip smileAnimation;
    [SerializeField] AnimationClip hitAnimation;
    [SerializeField] AnimationClip aggressiveAnimation;

    Animator animator;
    string winkTrigger = "wink";
    string hitTrigger = "hit";
    string smileTrigger = "smile";
    string aggressiveTrigger = "aggressive";
    string resetTrigger = "face_reset";
    
    public void INI(Animator animator, AnimatorOverrideController animatorOverride)
    {
        this.animator = animator;
        animatorOverride["null"] = defaultAnimation;
        animatorOverride["wink"] = winkAnimation;
        animatorOverride["smile"] = smileAnimation;
        animatorOverride["hit"] = hitAnimation;
        animatorOverride["aggressive"] = aggressiveAnimation;
        animator.runtimeAnimatorController = animatorOverride;
    }

    private IDisposable casualFace; 
    public void CasualFace()
    {
        casualFace?.Dispose();
        int blinkCount = 0;
        casualFace = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(3)).Subscribe((_) =>
        {
            blinkCount++;
            if (blinkCount % (10 / 3) == 0)
            {
                TriggerExpression(Facial.smile);
            }
            else
            {
                TriggerExpression(Facial.wink);
            }
        }).AddTo(gameObject);
    }
    
    public void TriggerExpression(Facial facial)
    {
        if (animator == null)
            return;
        switch (facial)
        {
            case Facial.hit:
                animator.SetTrigger(hitTrigger);
            break;
            case Facial.smile:
                animator.SetTrigger(smileTrigger);
            break;
            case Facial.aggressive:
                animator.SetTrigger(aggressiveTrigger);
            break;
            case Facial.wink:
                animator.SetTrigger(winkTrigger);
                break;
        }
    }
}

public enum Facial
{
    wink,
    hit,
    smile,
    aggressive
}
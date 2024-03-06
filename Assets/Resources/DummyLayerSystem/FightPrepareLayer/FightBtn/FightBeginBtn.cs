using System;
using UnityEngine;

public class FightBeginBtn : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private BOButton btn;

    public void SetAction(Action action)
    {
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action.Invoke);
    }
    
    public void Enable(bool on)
    {
        btn.interactable = on;
        animator.SetBool("On", on);
    }
}

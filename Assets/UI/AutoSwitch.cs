using System;
using UnityEngine;

public class AutoSwitch : MonoBehaviour
{
    const string AutoBool = "auto";
    const string AutoOnState = "AutoOn";
    const string AutoOffState = "AutoOff";

    [SerializeField] private BOButton btn;
    [SerializeField] private Animator animator;
    
    private Action<bool> _action;
    private Func<bool> _currentState;
    private bool startState;
    public Func<bool> CurrentState => _currentState;
    
    void OnEnable()
    {
        ApplyVisualState(startState, true);
    }
    
    void Switch(bool on)
    {
        animator.SetBool(AutoBool, on);
    }

    void ApplyVisualState(bool on, bool restartAnimation)
    {
        if (animator == null)
        {
            return;
        }

        Switch(on);
        if (restartAnimation)
        {
            animator.Play(on ? AutoOnState : AutoOffState, 0, 0f);
        }
        animator.Update(0f);
    }

    public void ChangeAutoState(bool on)
    {
        startState = on;
        _action?.Invoke(on);
        ApplyVisualState(on, true);
    }
    
    public void Initialize(Func<bool> state, Action<bool> action)
    {
        _action = action;
        _currentState = state;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            var changedState = !this._currentState();
            ChangeAutoState(changedState);
        });
        
        startState = this._currentState();
        ApplyVisualState(startState, true);
    }
}

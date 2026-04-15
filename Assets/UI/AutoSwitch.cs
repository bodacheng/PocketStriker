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
    private bool _hasStarted;
    private bool _hasPendingVisualState;
    private bool _pendingState;
    private bool _pendingRestartAnimation;
    public Func<bool> CurrentState => _currentState;
    
    void OnEnable()
    {
        ApplyOrQueueVisualState(startState, true);
    }

    void Start()
    {
        _hasStarted = true;
        FlushPendingVisualState();
    }
    
    void Switch(bool on)
    {
        animator.SetBool(AutoBool, on);
    }

    void ApplyOrQueueVisualState(bool on, bool restartAnimation)
    {
        if (animator == null)
        {
            return;
        }

        // During the first enable of an instantiated prefab, forcing Animator.Update(0)
        // can run before animated targets finish Awake.
        if (!_hasStarted)
        {
            _pendingState = on;
            _pendingRestartAnimation |= restartAnimation;
            _hasPendingVisualState = true;
            return;
        }

        ApplyVisualState(on, restartAnimation);
    }

    void FlushPendingVisualState()
    {
        if (!_hasPendingVisualState || !isActiveAndEnabled)
        {
            return;
        }

        var pendingState = _pendingState;
        var pendingRestartAnimation = _pendingRestartAnimation;
        _hasPendingVisualState = false;
        _pendingRestartAnimation = false;
        ApplyVisualState(pendingState, pendingRestartAnimation);
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
        ApplyOrQueueVisualState(on, true);
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
        ApplyOrQueueVisualState(startState, true);
    }
}

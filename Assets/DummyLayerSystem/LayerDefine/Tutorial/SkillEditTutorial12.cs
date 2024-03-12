using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SkillEditTutorial12 : MonoBehaviour
{
    [SerializeField] UnityEvent onOpenAction;
    [SerializeField] UnityEvent onCloseAction;
    void OnEnable()
    {
        onOpenAction.Invoke();
    }

    private void OnDisable()
    {
        onCloseAction.Invoke();
    }
}

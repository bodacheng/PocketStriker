using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LowerBarIcon : MonoBehaviour
{
    [SerializeField] private BOButton button;
    [SerializeField] private Animator animator;
    
    public BOButton BOButton => button;
    public Animator Animator => animator;
}

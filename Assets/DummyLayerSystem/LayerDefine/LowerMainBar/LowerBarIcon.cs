using UnityEngine;

public class LowerBarIcon : MonoBehaviour
{
    [SerializeField] private BOButton button;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject indicator;
    
    public BOButton BOButton => button;
    public Animator Animator => animator;
    public GameObject Indicator => indicator;
    
}

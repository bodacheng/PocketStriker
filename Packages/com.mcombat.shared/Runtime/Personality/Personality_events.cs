using UnityEngine;

public class Personality_events : MonoBehaviour
{
    [Header("剑")]
    [Space(1)]
    public GameObject right_sword,left_sword;

    void Start()
    {
        CloseAllPersonalityEffects();
    }
    
    public void CloseAllPersonalityEffects()
    {
        turn_off_Right_energy_blade();
        turn_off_Left_energy_blade();
    }

    public void turn_on_Right_energy_blade()
    {
        turnRightEnergyBlade(true);
    }
    public void turn_off_Right_energy_blade()
    {
        turnRightEnergyBlade(false);
    }
    void turnRightEnergyBlade(bool _on)
    {
        if (right_sword != null)
        {
            right_sword.SetActive(_on);
        }
    }
    
    public void turn_on_Left_energy_blade()
    {
        turnLeftEnergyBlade(true);
    }
    
    public void turn_off_Left_energy_blade()
    {
        turnLeftEnergyBlade(false);
    }
    
    void turnLeftEnergyBlade(bool _on)
    {
        if (left_sword != null)
        {
            left_sword.SetActive(_on);
        }
    }
}

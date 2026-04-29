using System;
using HittingDetection;

public partial class Data_Center
{
    public bool IsSubUnit => false;
    public bool HasChangedToSubUnit => false;
    public bool CanUseMainUnitDeathFlow => true;

    public void AssignSubUnitSwitcher(Func<string, V_Damage, bool> switcher)
    {
    }

    public void MarkChangedToSubUnit()
    {
    }

    public void ResetSubUnitState()
    {
    }

    public bool TryChangeToSub(string stateKey, V_Damage damage)
    {
        return false;
    }
}

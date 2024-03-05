using UnityEngine;
using HittingDetection;

public partial class Decomposition : MonoBehaviour
{
    Vector3 _tempPos;
    float _disFromCenter;
    public void Life()
    {
        if (Phase == 1 && IsWeapon)
        {
            switch (_HitBox._WeaponMode)
            {
                case WeaponMode.EnergyFromBodyWeapon:
                    if (_HitBox.weaponHP > 0 && _HitBox.CurrentHP <= 0)
                    {
                        CloseMarkers();
                        StopEmissions(false);
                        Counter = stop_emission_delay;
                        Phase = 2;
                    }
                    if (_HitBox.GetOwnerFACR() != null && _HitBox.GetOwnerFACR().GettingDamage)
                    {
                        CloseMarkers();
                        StopEmissions(false);
                        Counter = stop_emission_delay;
                        Phase = 2;
                    }
                    break;
                case WeaponMode.FlyerWeapon:
                    if (_HitBox.weaponHP > 0 && _HitBox.CurrentHP <= 0)
                    {
                        CloseMarkers();
                        StopEmissions(false);
                        Counter = stop_emission_delay;
                        Phase = 2;
                    }
                    break;
            }
        }
        
        switch (Phase)
        {
            case 1:
                if (DestructionDelay > 0 && stop_emission_delay > 0) //如果能量自身有寿命
                {
                    if (Counter > stop_emission_delay)
                    {
                        CloseMarkers();
                        StopEmissions(false);
                        Phase = 2;
                    }
                }
                break;
            case 2:
                if (DestructionDelay > 0 && stop_emission_delay > 0) //如果能量自身有寿命
                {
                    if (DestructionDelay > stop_emission_delay)
                    {
                        SetMaterialsAlpha((DestructionDelay - Counter) / (DestructionDelay - stop_emission_delay));
                    }
                }
                if (Counter > DestructionDelay || DestructionDelay <= 0) //DestructionDelay <= 0 代表这个物件没有生存寿命
                {
                    Phase = -1;
                }
                break;
            case -1: // -1是立刻归还对象池的flag。这个逻辑是让所有hitbox按序运行的重要一环。
                EnergyResolve();
                break;
        }
        
        if (FightGlobalSetting.SceneStep == 1)
        {
            _tempPos = transform.position;
            _tempPos.y = 0;
            _disFromCenter = _tempPos.magnitude;
            if (_disFromCenter > BoundaryControlByGod._BattleRingRadius + FightGlobalSetting._energyResolveAfterExtendBoundary)
            {
                Phase = -1;
            }
        }
        
        if (gameObject.activeSelf)
        {
            Counter += Time.deltaTime;
        }
    }
}

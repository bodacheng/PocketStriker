using UniRx;

public partial class FightParamsReference
{
    public ReactiveProperty<int> CriticalGauge { get; set; } = new ReactiveProperty<int>();
    public ReactiveProperty<int> DreamComboGauge { get; set; } = new ReactiveProperty<int>();
        
    public void PlusEx(int add)
    {
        CriticalGauge.Value = CombatGaugeUtility.PlusGauge(CriticalGauge.Value, add, FightGlobalSetting._EXMax);
    }

    public void PlusDreamGauge(int add)
    {
        DreamComboGauge.Value = CombatGaugeUtility.PlusDreamGauge(
            Center._MyBehaviorRunner.OnFixedSequence,
            DreamComboGauge.Value,
            add,
            FightGlobalSetting._DreamComboGaugeMax);
    }
    
    public void CostCriticalGaugeBySPLevel(int level)
    {
        CriticalGauge.Value = CombatGaugeUtility.CostCriticalGaugeBySPLevel(
            CriticalGaugeMode,
            Center._MyBehaviorRunner.OnFixedSequence,
            CriticalGauge.Value,
            FightGlobalSetting._EXMax,
            level);
    }
    
    public bool HasPlentyGauge(int spLevel)
    {
        return CombatGaugeUtility.HasPlentyGauge(CriticalGaugeMode, CriticalGauge.Value, spLevel);
    }

    public bool HasPlentyDreamGauge()
    {
        return CombatGaugeUtility.HasPlentyDreamGauge(
            DreamComboGauge.Value,
            FightGlobalSetting._DreamComboGaugeMax);
    }
}

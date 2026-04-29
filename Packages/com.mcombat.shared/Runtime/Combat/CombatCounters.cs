using UniRx;
using UnityEngine;

public static class CombatGaugeUtility
{
    public static int PlusGauge(int current, int add, int max)
    {
        return Mathf.Clamp(current + add, 0, max);
    }

    public static int PlusDreamGauge(bool onFixedSequence, int current, int add, int max)
    {
        return onFixedSequence ? current : PlusGauge(current, add, max);
    }

    public static int CostCriticalGaugeBySPLevel(
        CriticalGaugeMode criticalGaugeMode,
        bool onFixedSequence,
        int current,
        int max,
        int spLevel)
    {
        if (criticalGaugeMode == CriticalGaugeMode.Unlimited || onFixedSequence)
        {
            return current;
        }

        return PlusGauge(current, -CriticalGaugeCostBySPLevel(spLevel), max);
    }

    public static bool HasPlentyGauge(CriticalGaugeMode criticalGaugeMode, int current, int spLevel)
    {
        return criticalGaugeMode == CriticalGaugeMode.Unlimited
               || current >= CriticalGaugeCostBySPLevel(spLevel);
    }

    public static bool HasPlentyDreamGauge(int current, int max)
    {
        return current >= max;
    }

    static int CriticalGaugeCostBySPLevel(int spLevel)
    {
        return spLevel switch
        {
            1 => 30,
            2 => 60,
            3 => 90,
            _ => 0
        };
    }
}

public class ComboHitCount
{
    public ReactiveProperty<int> HitCount { get; set; } = new ReactiveProperty<int>(0);
    readonly float hitConnectTolerate;
    float hitComboTimeCounter;

    public ComboHitCount()
    {
        HitCount.Value = 0;
        hitConnectTolerate = 4f;
        hitComboTimeCounter = 0f;
    }

    public void HitCountPlus(BeHitCount beHitCount)
    {
        hitComboTimeCounter = hitConnectTolerate;
        HitCount.Value += 1;
        beHitCount.BeHitCountInterrupt();
    }

    public void HitCountInterrupt()
    {
        HitCount.Value = 0;
        hitComboTimeCounter = 0;
    }

    public void Update()
    {
        if (hitComboTimeCounter <= 0f)
        {
            return;
        }

        hitComboTimeCounter -= Time.fixedDeltaTime;
        if (hitComboTimeCounter <= 0f)
        {
            HitCount.Value = 0;
        }
    }
}

public class KnockOffCount
{
    float knockOffTimeCounter;
    readonly float knockOffCooldownInfer;
    float knockOffGauge;

    public KnockOffCount()
    {
        knockOffTimeCounter = 1f;
        knockOffCooldownInfer = 1f;
        knockOffGauge = 0;
    }

    public void SetGauge(float amount)
    {
        knockOffGauge = amount;
    }

    public void PlusGauge(float amount)
    {
        knockOffGauge += amount;
    }

    public void PlusTimeCounter(float timeAmount)
    {
        knockOffTimeCounter += timeAmount;
    }

    public float GetGauge()
    {
        return knockOffGauge;
    }

    public void Update()
    {
        if (knockOffGauge <= 0)
        {
            return;
        }

        if (knockOffTimeCounter > 0)
        {
            knockOffTimeCounter -= Time.fixedDeltaTime;
        }

        if (knockOffTimeCounter <= 0)
        {
            knockOffGauge = Mathf.Clamp(knockOffGauge - 2f, 0, Mathf.Infinity);
            knockOffTimeCounter = knockOffCooldownInfer;
        }
    }
}

public class BeHitCount
{
    int beHitCount;
    float beHitComboTimeCounter;
    readonly float hitConnectTolerate;

    public BeHitCount()
    {
        beHitCount = 0;
        hitConnectTolerate = 1.5f;
        beHitComboTimeCounter = 0f;
    }

    public void BeHitCountPlus()
    {
        beHitComboTimeCounter = hitConnectTolerate;
        beHitCount += 1;
    }

    public void BeHitCountInterrupt()
    {
        beHitCount = 0;
        beHitComboTimeCounter = 0;
    }

    public int GetBeHitCount()
    {
        return beHitCount;
    }

    public void Update()
    {
        if (beHitComboTimeCounter <= 0f)
        {
            return;
        }

        beHitComboTimeCounter -= Time.fixedDeltaTime;
        if (beHitComboTimeCounter <= 0f)
        {
            beHitCount = 0;
        }
    }
}

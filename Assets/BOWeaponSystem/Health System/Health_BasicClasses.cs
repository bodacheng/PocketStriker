using UniRx;
using UnityEngine;

public class KnockOffCount
{
    float knock_off_time_counter;
    readonly float knock_off_cooldown_infer;
    float knock_off_gauge;

    public KnockOffCount()
    {
        knock_off_time_counter = 1f;
        knock_off_cooldown_infer = 1f;
        knock_off_gauge = 0;
    }

    public void SetGauge(float amount)
    {
        knock_off_gauge = amount;
    }

    public void PlusGauge(float amount)
    {
        knock_off_gauge += amount;
    }

    public void PlusTimeCounter(float timeamout)
    {
        knock_off_time_counter += timeamout;
    }

    public float GetGauge()
    {
        return knock_off_gauge;
    }

    public void Update()
    {
        //底下这些就是说，我每隔一段时间就让KockoFF槽冷却一些(降低2)
        if (knock_off_gauge > 0)
        {
            if (knock_off_time_counter > 0)
            {
                knock_off_time_counter -= Time.fixedDeltaTime;
            }
            if (knock_off_time_counter <= 0)
            {
                knock_off_gauge = Mathf.Clamp(knock_off_gauge - 2f, 0, Mathf.Infinity);
                knock_off_time_counter = knock_off_cooldown_infer;
            }
        }
    }
}

// instance主被攻击的计数。如果能够反击一次敌人的话该计数会被立刻清零
public class BeHitCount
{
    int beHitCount;
    float BeHitComboTimeCounter;
    readonly float HitConnectTolerate;
    
    public BeHitCount()
    {
        beHitCount = 0;
        HitConnectTolerate = 1.5f;
        BeHitComboTimeCounter = 0f;
    }
    
    public void BeHitCountPlus()
    {
        BeHitComboTimeCounter = HitConnectTolerate;
        beHitCount += 1;
    }

    // 成功反击别人一次的话，自己被连续揍的次数清零。
    public void BeHitCountInterrupt()
    {
        beHitCount = 0;
        BeHitComboTimeCounter = 0;
    }

    public int GetBeHitCount()
    {
        return beHitCount;
    }

    public void Update()
    {
        if (BeHitComboTimeCounter > 0f)
        {
            BeHitComboTimeCounter -= Time.fixedDeltaTime;
            if (BeHitComboTimeCounter <= 0f)
            {
                beHitCount = 0;
            }
        }
    }
}

public class ComboHitCount
{
    public ReactiveProperty<int> HitCount{ get; set; } = new ReactiveProperty<int>(0);
    readonly float HitConnectTolerate;
    float HItComboTimeCounter;

    public ComboHitCount()
    {
        HitCount.Value = 0;
        HitConnectTolerate = 4f;
        HItComboTimeCounter = 0f;
    }

    public void HitCountPlus(BeHitCount beHitCount)
    {
        HItComboTimeCounter = HitConnectTolerate;
        HitCount.Value += 1;
        beHitCount.BeHitCountInterrupt();
    }

    public void HitCountInterrupt()
    {
        HitCount.Value = 0;
        HItComboTimeCounter = 0;
    }

    public void Update()
    {
        if (HItComboTimeCounter > 0f)
        {
            HItComboTimeCounter -= Time.fixedDeltaTime;
            if (HItComboTimeCounter <= 0f)
            {
                HitCount.Value = 0;
            }
        }
    }
}

using UniRx;

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

        hitComboTimeCounter -= UnityEngine.Time.fixedDeltaTime;
        if (hitComboTimeCounter <= 0f)
        {
            HitCount.Value = 0;
        }
    }
}

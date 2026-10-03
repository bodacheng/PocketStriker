using System;

/// <summary>One automatic interstitial placement for each completed battle attempt.</summary>
public sealed class PostBattleAdSession
{
    private bool eligible;
    private bool completed;
    private int attempt;

    public bool Pending { get; private set; }

    public static bool IsEligible(FightEventType eventType, string stageId, bool runTutorial = false)
    {
        if (runTutorial) return false;

        switch (eventType)
        {
            case FightEventType.Quest:
                return !int.TryParse(stageId, out var stage) || stage < 1 || stage > 5;
            case FightEventType.Arena:
            case FightEventType.Gangbang:
            case FightEventType.Event:
                return true;
            default:
                return false;
        }
    }

    public void BeginBattle(FightEventType eventType, string stageId, bool runTutorial)
    {
        attempt++;
        eligible = IsEligible(eventType, stageId, runTutorial);
        completed = false;
        Pending = false;
    }

    public void CompleteBattle()
    {
        if (completed) return;
        completed = true;
        Pending = eligible;
    }

    public void Cancel()
    {
        attempt++;
        Pending = false;
        completed = true;
    }

    public bool TryPresent(bool resultActive, bool adFree, bool adReady,
        bool anotherAdShowing, Func<bool> show)
    {
        if (!Pending) return false;
        if (!resultActive || adFree)
        {
            Cancel();
            return false;
        }
        if (!adReady || anotherAdShowing) return false;

        // Consume before SDK callbacks run, including synchronous callbacks.
        var currentAttempt = attempt;
        Pending = false;
        var presented = show();
        if (!presented && currentAttempt == attempt) Pending = true;
        return presented;
    }
}

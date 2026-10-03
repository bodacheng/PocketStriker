using UnityEngine;

/// <summary>One captured finger controls orbit; another finger can never replace it mid-drag.</summary>
public sealed class BattleArenaOrbitGesture
{
    public const float DegreesPerViewport = 180;
    public int? ActiveFingerId { get; private set; }
    Vector2 _previousPosition;

    public float UpdatePointer(int fingerId, TouchPhase phase, Vector2 position, bool overUI, float viewportWidth)
    {
        if (phase == TouchPhase.Began)
        {
            if (ActiveFingerId == fingerId && overUI) Reset();
            if ((!ActiveFingerId.HasValue || ActiveFingerId == fingerId) && !overUI)
            {
                ActiveFingerId = fingerId;
                _previousPosition = position;
            }
            return 0;
        }
        if (ActiveFingerId != fingerId) return 0;
        if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled || overUI)
        {
            Reset();
            return 0;
        }
        float delta = phase == TouchPhase.Moved ? position.x - _previousPosition.x : 0;
        _previousPosition = position;
        return delta / Mathf.Max(1, viewportWidth) * DegreesPerViewport;
    }

    public void Reset() => ActiveFingerId = null;
}

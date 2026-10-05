using UnityEngine;

public class HitBoxSubEventManger : MonoBehaviour
{
    [SerializeField] Decomposition decomposition;
    [SerializeField] EventAndTriggerTime _event;
    [SerializeField] string LandedEvent;
    [SerializeField] string fadeEvent;

    float _timeCount;
    bool clockEventTriggered, landEventTriggered, fadeEventTriggered;

    void OnEnable()
    {
        _timeCount = 0;
        clockEventTriggered = landEventTriggered = fadeEventTriggered = false;
    }

    bool CanProcess => decomposition != null && !decomposition.IsBattleEffectInvalidated
        && (decomposition.Phase == 1 || decomposition.Phase == 2)
        && !BattleEffectLifetime.Suspended && isActiveAndEnabled;

    // Decomposition.Life owns the ordering, so a split scheduled at the same
    // instant as its parent's destruction is emitted before pool return.
    public void ProcessEvents(float deltaTime)
    {
        if (!CanProcess) return;
        _timeCount += deltaTime;
        if (!clockEventTriggered && _event != null && !string.IsNullOrEmpty(_event.event_name)
            && _timeCount >= _event.time)
        {
            clockEventTriggered = true;
            decomposition.SpecialTriggerEvent(_event.event_name, this);
        }

        if (!CanProcess) return;
        if (!landEventTriggered && !string.IsNullOrEmpty(LandedEvent)
            && decomposition.transform.position.y <= 0)
        {
            landEventTriggered = true;
            decomposition.SpecialTriggerEvent(LandedEvent, this);
            decomposition.Phase = -1;
        }

        if (!CanProcess) return;
        if (!fadeEventTriggered && !string.IsNullOrEmpty(fadeEvent)
            && decomposition._HitBox != null && decomposition._HitBox.weaponHP > 0
            && decomposition._HitBox.CurrentHP <= 0)
        {
            fadeEventTriggered = true;
            decomposition.SpecialTriggerEvent(fadeEvent, this);
        }
    }

    [System.Serializable]
    public class EventAndTriggerTime
    {
        public float time;
        public string event_name;
    }
}

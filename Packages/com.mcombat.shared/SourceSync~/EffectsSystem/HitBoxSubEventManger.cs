using UnityEngine;
using UniRx;

public class HitBoxSubEventManger : MonoBehaviour
{
    [SerializeField] Decomposition decomposition;
    [SerializeField] EventAndTriggerTime _event;
    [SerializeField] string LandedEvent;
    [SerializeField] string fadeEvent;
    
    float _timeCount;
    SingleAssignmentDisposable _clockEvent, _landEvent, _fadedEvent;

    void DisposeEvents()
    {
        if (_clockEvent != null && !_clockEvent.IsDisposed)
            _clockEvent.Dispose();
        if (_landEvent != null && !_landEvent.IsDisposed)
            _landEvent.Dispose();
        if (_fadedEvent != null && !_fadedEvent.IsDisposed)
            _fadedEvent.Dispose();
    }
    
    void OnDestroy()
    {
        DisposeEvents();
    }
    
    void OnDisable()
    {
        DisposeEvents();
    }

    void OnEnable()
    {
        _timeCount = 0;        
        if (!string.IsNullOrEmpty(_event.event_name))
        {
            _clockEvent = new SingleAssignmentDisposable();
            _clockEvent.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (_timeCount > _event.time)
                    {
                        decomposition.SpecialTriggerEvent(_event.event_name, this);
                        _clockEvent.Dispose();
                    }
                    if (!gameObject.activeSelf)
                    {
                        _clockEvent.Dispose();
                    }
                }
            );
            SingleAssignmentDisposableCleaner.Add(_clockEvent);
        }
        
        if (!string.IsNullOrEmpty(LandedEvent))
        {
            _landEvent = new SingleAssignmentDisposable();
            _landEvent.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (decomposition.transform.position.y <= 0)
                    {
                        decomposition.SpecialTriggerEvent(LandedEvent, this);
                        decomposition.Phase = -1;
                        _landEvent.Dispose();
                    }
                    
                    if (!gameObject.activeSelf)
                    {
                        _landEvent.Dispose();
                    }
                }
            );
            SingleAssignmentDisposableCleaner.Add(_landEvent);
        }
        
        if (!string.IsNullOrEmpty(fadeEvent))
        {
            _fadedEvent = new SingleAssignmentDisposable();
            _fadedEvent.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (decomposition._HitBox.weaponHP > 0 && decomposition._HitBox.CurrentHP <= 0)
                    {
                        decomposition.SpecialTriggerEvent(fadeEvent, this);
                        _fadedEvent.Dispose();
                    }
                    if (!gameObject.activeSelf)
                    {
                        _fadedEvent.Dispose();
                    }
                }
            );
            SingleAssignmentDisposableCleaner.Add(_fadedEvent);
        }
    }
    
    void Update()
    {
        _timeCount += Time.deltaTime;
    }
    
    [System.Serializable]
    public class EventAndTriggerTime
    {
        public float time;
        public string event_name;
    }
}
using UnityEngine;

public class TrackControl : MonoBehaviour {
    
    //public List<EventKeyframe> listEventKeyframe = new List<EventKeyframe>();
    
    public TrackMode _TrackMode = TrackMode.DefinedTrack;
    
    [Space(10)]
    [Header("DefinedTrack")]
    [SerializeField] AnimationCurve xAnimationCurve;
    [SerializeField] AnimationCurve zAnimationCurve;
    [SerializeField] AnimationCurve yAnimationCurve;
    [SerializeField] float Z_scale = 1f;
    Matrix4x4 m;

    [Space(10)]
    [Header("Navigation")]
    [SerializeField] float navi_time = 5f;
    [SerializeField] float DegreeOfControl = 1;
    [SerializeField] float navRunSpeed = 10;

    public Sensor Sensor
    {
        get;
        set;
    }
    Vector3 direction;
    Transform navTarget;
    float time_counter;
    
    public void StartOff(Vector3 start, Quaternion startQ, float _Z_scale)
    {
        time_counter = 0;
        switch (_TrackMode)
        {
            case TrackMode.DefinedTrack:
                this.Z_scale = _Z_scale;
                m = Matrix4x4.TRS(start, startQ, Vector3.one);
            break;
            case TrackMode.Navigation:
                m = Matrix4x4.TRS(start, startQ, Vector3.one * 1);
                direction = m.MultiplyPoint3x4(new Vector3(0,0,1)) - m.MultiplyPoint3x4(new Vector3(0,0,0));
                transform.position = start;
            break;
        }
    }

    Transform nav_target;
	void Update()
	{
        time_counter += Time.deltaTime;
        switch(_TrackMode)
        {
            case TrackMode.DefinedTrack:
                transform.position = m.MultiplyPoint3x4(new Vector3(xAnimationCurve.Evaluate( time_counter ), yAnimationCurve.Evaluate(time_counter), zAnimationCurve.Evaluate( time_counter ) * Z_scale ));
            break;
            case TrackMode.Navigation:
                if (time_counter < navi_time)
                {
                    if (Sensor != null)
                    {
                        if (Sensor.GetClosestEnemyColliderInSensorRange() != null)
                        {
                            nav_target = Sensor.GetClosestEnemyColliderInSensorRange().transform;
                        }
                        else if (Sensor.GetEnemiesByDistance(false).Count > 0)
                        {
                            nav_target = Sensor.GetEnemiesByDistance(false)[0].transform;
                        }
                        
                        if (nav_target != null)
                        {
                            direction += (nav_target.position - transform.position).normalized * Time.deltaTime * DegreeOfControl;
                        }
                    }
                }
                
                direction.y = 0;
                direction = direction.normalized;
                transform.position = Vector3.Lerp(transform.position,transform.position + direction * navRunSpeed, Time.deltaTime);
            break;
        }
	}

    public enum TrackMode
    {
        DefinedTrack = 1,
        Navigation = 2
    }
}
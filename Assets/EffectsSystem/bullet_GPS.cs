//using System.Collections.Generic;
//using UnityEngine;
//using System.Linq;

//public class Bullet_GPS : MonoBehaviour {

//    public float navigation_time;
//    public float force;
//    public int sensorRate;
//    public float sensorRange;
//    public LayerMask layerMask;
    
//    List<Collider> _hits = new List<Collider>();
//    float time_counter;
    
//    public void Local_OnEnable()
//	{
//        time_counter = 0f;
//    }

//	void FixedUpdate()
//	{
//        time_counter += Time.fixedDeltaTime;
//        if (time_counter < navigation_time)
//        {
//            if (_hits.Count > 0)
//            {
//                //_hits.Sort((Collider a, Collider b) => DistanceCompare(a, b));
//                if (_hits[0] != null)
//                {
//                    ToTargetDirection(_hits[0].transform.position, force, true);
//                }
//            }
//        }
//        if (sensorRate >= 5)
//        {
//            _hits = Physics.OverlapSphere(transform.position, sensorRange, layerMask).ToList();//这个东西消耗太大，起码可以考虑减少运行次数
//            sensorRate = 0;
//        }
//        sensorRate++;
//	}

//	protected void ToTargetDirection(Vector3 target, float force, bool ignoreY)
//    {
//        Vector3 look_dir = target - gameObject.transform.position;
//        if (ignoreY)
//        {
//            look_dir.y = 0;
//        }
//        transform.position = Vector3.Lerp(transform.position,target, force * Time.fixedDeltaTime);
//    }
    
//    float p1_to_me, p2_to_me;
//    public int DistanceCompare(Collider p1, Collider p2)
//    {
//        if (p1 == null || p2 == null)
//        {
//            return 0;
//        }
//        p1_to_me = (p1.gameObject.transform.position - gameObject.transform.position).magnitude;
//        p2_to_me = (p2.gameObject.transform.position - gameObject.transform.position).magnitude;
//        return p1_to_me > p2_to_me ? 1 : p1_to_me < p2_to_me ? -1 : 0;
//    }
//}
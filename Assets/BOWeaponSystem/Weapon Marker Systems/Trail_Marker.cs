using UnityEngine;

namespace HittingDetection
{
    public class Trail_Marker : Marker
    {
        public RaycastHit[] _hits = new RaycastHit[0];
        
        // trail detect
        Vector3 _lastFramePos; //Temporary position of the marker from the last frame
        float _dist; //distance between temp and actual marker position
        Vector3 _dir; //Direction of the above.

        public override void LocalAwake()
        {
            base.LocalAwake();
            _lastFramePos = transform.position;
            myCollider.radius = 0.1f;//这个是为了什么呢。。比如剑，它如果没有一个小collider的话那不是不好被其他角色检测来躲闪吗？
            myCollider.isTrigger = true;
        }

        public override bool HitCheck()
        {
            _dir = transform.position - _lastFramePos;
            _dist = Vector3.Distance(transform.position, _lastFramePos);
            //Debug.DrawRay(_tempPos, _dir, Color.white, 0.3f);
            _hits = Physics.RaycastAll(_lastFramePos, _dir, _dist, _layers, QueryTriggerInteraction.Collide);
            if (_hits.Length == 0)
            {
                _lastFramePos = transform.position;
            }
            return _hits.Length > 0;
        }
        
        public override void EnableMarkerProcess(int weaponLayer)
        {
            _lastFramePos = transform.position;
            base.EnableMarkerProcess(weaponLayer);
        }
        
        public override void DisableMarkerProcess()
        {
            base.DisableMarkerProcess();
            _lastFramePos = transform.position;
        }
        
        public override void ClearMarkerProcess()
        {
            _lastFramePos = transform.position;
            ClearDetection();
        }
        
        protected override void ClearDetection()
        {
            _hits = new RaycastHit[0];
        }
    }
}


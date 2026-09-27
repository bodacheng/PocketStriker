using UnityEngine;

namespace HittingDetection
{
    public class Trail_Marker : Marker
    {
        RaycastHit[] _hitBuffer = new RaycastHit[8];
        public RaycastHit[] _hits => _hitBuffer;
        public int HitCount { get; private set; }
        
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
            if (_dist <= Mathf.Epsilon)
            {
                HitCount = 0;
                return false;
            }

            var rayDirection = _dir / _dist;
            HitCount = Physics.RaycastNonAlloc(_lastFramePos, rayDirection, _hitBuffer, _dist, _layers, QueryTriggerInteraction.Collide);
            if (HitCount == _hitBuffer.Length)
            {
                _hitBuffer = new RaycastHit[_hitBuffer.Length * 2];
                HitCount = Physics.RaycastNonAlloc(_lastFramePos, rayDirection, _hitBuffer, _dist, _layers, QueryTriggerInteraction.Collide);
            }
            if (HitCount == 0)
            {
                _lastFramePos = transform.position;
            }
            return HitCount > 0;
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
            HitCount = 0;
        }
    }
}

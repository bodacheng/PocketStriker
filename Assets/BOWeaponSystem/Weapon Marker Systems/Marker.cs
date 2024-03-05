//The Marker Script
//All markers must be a child of Marker Manager

using UnityEngine;

namespace HittingDetection
{
    public class HitPointPara
    {
        public Vector3 onBodyPos;// 生成特效位置
        public Vector3 impactPos;// 冲击来源点
        public Quaternion qua;
        public float WeaponHpCost;
        public bool exhaustEffect = false;
    }
    
    public class Marker : MonoBehaviour
    {
        [Tooltip("Choose which Layers should be affected by this marker's hit check.")]
        public LayerMask _layers;
        public LayerMask enemyShieldLayer;
        
        protected SphereCollider myCollider;
        protected HitBoxManager owner;
        
        public void SetOwner(HitBoxManager b)
        {
            this.owner = b;
        }
        
        public virtual bool HitCheck()
        {
            return false;
        }

        public virtual void LocalAwake()
        {
            myCollider = gameObject.GetComponent<SphereCollider>();
            if (myCollider == null)
            {
                myCollider = gameObject.AddComponent<SphereCollider>();
            }
        }
        
        public virtual void EnableMarkerProcess(int weaponLayer)
        {
            gameObject.layer = weaponLayer;
        }
        
        public virtual void DisableMarkerProcess()
        {
            ClearDetection();
            gameObject.layer = 0;
        }
        
        public virtual void ClearMarkerProcess()
        {
        }
        
        protected virtual void ClearDetection()
        {
        }
        
        protected virtual void OnTriggerEnter(Collider other)
        {
        }
        
        protected virtual void OnTriggerStay(Collider other)
        {
        }
    }
}
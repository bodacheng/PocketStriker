using UnityEngine;

public class BO_Limb : MonoBehaviour
{
    [Tooltip("What is this Limb attached to? Select the desired BS Health script which stores the Health of this Object (for example in one of the limb's parents.")]
    public Data_Center Center;
    [Tooltip("ColliderOfThisHitBox.MUST HAVE")]
    public Collider myColliderMustEquip;

    void Start()
    {
        if (Center != null)
            Center.FightDataRef.RegisterLimb(this);
        else
        {
            Debug.Log("存在未启用的BO_Limb"+ gameObject);
        }
    }

    public void INI()
    {
        if (GetComponent<Collider>())
        {
            myColliderMustEquip = GetComponent<Collider>();
        }
        else
        {
            Debug.Log("hitbox" + transform + "没有配置collider，并不会自动创建，请检查");
            enabled = false;
            return;
        }

        if (Center == null)
        {
            Debug.Log("hitbox" + this.gameObject.name + "由于没有适配Center而将尝试关闭");
            enabled = false;
            return;
        }
    }
        
    public bool IfStepOnEnemy(Collider box)
    {
        if (box.isTrigger)
            return false;
        return
        Center._TeamConfig.enemyLayerMask == (Center._TeamConfig.enemyLayerMask | (1 << box.gameObject.layer)) || 
        (Center._TeamConfig.enemyShieldLayerMask & (1 << box.gameObject.layer)) != 0;
    }
}
    //void OnTriggerExit(Collider other)
    //{
        //if (other.gameObject.layer == 19)
        //{
        //    GetComponent<Collider>().isTrigger = false;
        //    Debug.Log("出地");
        //}
    //}

    //void OnTriggerEnter(Collider collision)//这个是针对角色倒地
    //{
        //if (enable)
        //{
        //    if (AI_DATA_CENTER == null)
        //        return;

        //    if (AI_DATA_CENTER._loadMode == loadMode.fightModel)
        //    {
        //        if (MainHealth.ifStepOnEnemyCharacter(collision.GetComponent<Collider>()))
        //        {
        //            this.MainHealth.WhenIHitSomethingEnemy(1);
        //        }
        //    }
        //}
    //}

    //void OnCollisionStay(Collision collision)
    //{
        //if (enable)
        //{
        //    if (AI_DATA_CENTER == null)
        //        return;
        //    if (collision.collider.gameObject.layer == 13)
        //    {
        //        if (AI_DATA_CENTER)
        //            AI_DATA_CENTER.animator.applyRootMotion = false;

        //        Vector3 to = collision.collider.transform.right * 3f;
        //        to.y = 0;
        //        myRigidbody.AddForce(to * 1f, ForceMode.VelocityChange);
        //    }
        //}
    //}
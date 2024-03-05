using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using HittingDetection;
using UnityEngine.Animations;
using Log;

public class BO_Weapon_Animation_Events : MonoBehaviour
{
    public HiddenMethods hiddenMethods;
    
    TeamConfig _TeamConfig = TeamConfig.DefaultSet;
    readonly List<Transform> _Used_Targets = new List<Transform>();
    IDictionary<Transform, Decomposition> bodyPartsHitBoxRegisterDic;
    Transform right_hand, left_hand, right_foot, left_foot, head, tail;
    Transform geometryCenter;
    FightParamsReference myownheath;

    void Awake()
    {
        hiddenMethods = new HiddenMethods(this);
    }
    
    public class HiddenMethods
    {
        readonly BO_Weapon_Animation_Events BEs;
        
        public HiddenMethods(BO_Weapon_Animation_Events bO_Weapon_Animation_Events)
        {
            BEs = bO_Weapon_Animation_Events;
        }
        
        public void AssignTeamFlag(TeamConfig teamConfig)
        {
            BEs._TeamConfig = teamConfig;
        }
        
        public void AssignWeaponsFromDataCenter(FightParamsReference Ownheath, Transform geometryCenter, Transform right_hand, Transform left_hand, Transform right_foot, Transform left_foot, Transform head, Transform tail)
        {
            BEs.myownheath = Ownheath;
            BEs.geometryCenter = geometryCenter;
            BEs.right_hand = right_hand;
            BEs.left_hand = left_hand;
            BEs.right_foot = right_foot;
            BEs.left_foot = left_foot;
            BEs.head = head;
            BEs.tail = tail;
            BEs.bodyPartsHitBoxRegisterDic = new Dictionary<Transform, Decomposition>();
            if (BEs.right_hand != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.right_hand,null);
            if (BEs.left_hand != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.left_hand,null);
            if (BEs.left_foot != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.left_foot,null);
            if (BEs.right_foot != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.right_foot,null);
            if (BEs.head != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.head,null);
            if (BEs.tail != null)
                BEs.bodyPartsHitBoxRegisterDic.Add(BEs.tail,null);
        }
        
        //注意看0被空出来是和添加 删除有效武器列表中的0参数有关
        void SetThisWeaponDamageTypeByNum(int heavyNum, HitBoxManager weapon)
        {
            var damageType = V_Damage.FormalIntToDamageType(heavyNum);
            weapon.SetDamageType(damageType);
        }
        
        void RegisterBodyPartWeapon(Transform t)
        {
            Decomposition decomposition = null;
            if (t != null && !BEs.bodyPartsHitBoxRegisterDic.ContainsKey(t))
            {
                BEs.bodyPartsHitBoxRegisterDic.Add(t, null);
            }
            if (BEs.bodyPartsHitBoxRegisterDic[t] == null)
            {
                decomposition = HurtObjectManager.GetDPool().Rent();
                BEs.bodyPartsHitBoxRegisterDic[t] = decomposition;
                decomposition._HitBox.SetOwnerFACR(BEs.myownheath);
            }
            else
            {
                decomposition = BEs.bodyPartsHitBoxRegisterDic[t];
            }
            ConstraintSource myConstraintSource = new ConstraintSource
            {
                sourceTransform = t,
                weight = 1
            };
            decomposition.transform.position = t.position;
            decomposition.GetPositionConstraint().SetSources(new List<ConstraintSource> { myConstraintSource });
            decomposition.GetPositionConstraint().constraintActive = true;
            decomposition.GetPositionConstraint().locked = true;
            decomposition._HitBox.SetTeamConfig(BEs._TeamConfig);
            decomposition._HitBox.SetReferenceTransformInfo(BEs.geometryCenter);//第二个参数是因为BE本身就在wholeT上
            decomposition._HitBox.SetDetectionTargetsUnion(BEs._Used_Targets);
            decomposition._HitBox.MarkersEnablingStarts();
            
            if (FightGlobalSetting.HitBoxLogger)
            {
                decomposition._HitBox.GeneratedByStateKey = BEs.myownheath.Center._MyBehaviorRunner.GetNowState().StateKey;
                decomposition._HitBox.HitBoxLifeEnding = HitBoxLifeEnding.untouched;
            }
        }
        
        public void RemoveBodyPartWeapon(Transform t)
        {
            Decomposition hitBox = BEs.bodyPartsHitBoxRegisterDic[t];
            BEs.bodyPartsHitBoxRegisterDic[t] = null;
            if (hitBox != null)
            {
                hitBox.GetPositionConstraint().constraintActive = false;
                hitBox._HitBox.SetDetectionTargetsUnion(null);
                hitBox.Phase = -1;
            }
        }
        
        public void RegisterBodyPartWeapon(Transform t, int hit_type) // hit_type == 0: clear ;hit_type != 0 : in
        {
            if (hit_type != 0)
            {
                RegisterBodyPartWeapon(t);
                SetThisWeaponDamageTypeByNum(hit_type, BEs.bodyPartsHitBoxRegisterDic[t]._HitBox);
            }
            else
            {
                RemoveBodyPartWeapon(t);
            }
        }
    }
    
    // 当前这个版本因为把“身体固化武器”的检测对象给联合化了。。其实关于_Used_Targets清理，
    // 在以下的ClearTargets()DisableMarkers ()EnableMarkers()
    // 三个函数中都存在重复执行，都把本模块内的_Used_Targets给多次执行clear操作了。
    // 因为武器在作为飞行道具，伤害性特效的情况下，是自己保有一个单独的_Used_Targets列表，也是靠这三个函数来连带着对其进行清空
    // 所以找不太到一种能更好统合固化武器，特效武器这方面的写法，所以干脆就保持着这个让_Used_Targets重复被clear的状态。
    readonly List<Transform> toRefreshParts = new List<Transform>();
    public void ClearTargets()
	{
        toRefreshParts.Clear();
        foreach (KeyValuePair<Transform, Decomposition> keyValuePair in bodyPartsHitBoxRegisterDic) 
        {
            if (keyValuePair.Value != null)
            {
                if (keyValuePair.Value._HitBox.weaponHP < 0) // weaponHP >= 0的武器会在HP下降的同时自动  ClearTargets
                {
                    keyValuePair.Value._HitBox.ClearTargets();
                }
                else
                {
                    if (keyValuePair.Value._HitBox.CurrentHP <= 0) // 来自本模块的ClearTargets意味着hitbox的刷新。
                    {
                        toRefreshParts.Add(keyValuePair.Key);
                    }
                }
            }
        }
        for (int i = 0; i < toRefreshParts.Count; i++)
        {
            hiddenMethods.RemoveBodyPartWeapon(toRefreshParts[i]);
            hiddenMethods.RegisterBodyPartWeapon(toRefreshParts[i], 1);
        }
    }

    List<Transform> bodyparts;
    public void ClearMarkerManagers()
    {
        bodyparts = bodyPartsHitBoxRegisterDic.Keys.ToList();
        for (int i = 0; i < bodyparts.Count; i++)
        {
            if (bodyparts[i] != null)
            {
                hiddenMethods.RemoveBodyPartWeapon(bodyparts[i]);
            }
        }
    }
    
	public void EnableMarkers()
	{
        toRefreshParts.Clear();
        foreach (KeyValuePair<Transform,Decomposition> keyValuePair in bodyPartsHitBoxRegisterDic) 
        {
            if (keyValuePair.Value != null)
            {
                if (keyValuePair.Value._HitBox.weaponHP > 0 && keyValuePair.Value._HitBox.CurrentHP <= 0) // 来自本模块的ClearTargets意味着hitbox的刷新。
                {
                    toRefreshParts.Add(keyValuePair.Key);
                }else{
                    keyValuePair.Value._HitBox.EnableMarkers();
                }
            }
        }
        for (int i = 0; i < toRefreshParts.Count; i++)
        {
            hiddenMethods.RemoveBodyPartWeapon(toRefreshParts[i]);
            hiddenMethods.RegisterBodyPartWeapon(toRefreshParts[i], 1);
        }
	}
    
    public void DisableMarkers()
    {
        foreach (KeyValuePair<Transform,Decomposition> keyValuePair in bodyPartsHitBoxRegisterDic) 
        {
            if (keyValuePair.Value != null)
            {
                keyValuePair.Value._HitBox.DisableMarkers();
            }
        }
    }
    
    DamageType damageType;
    public void SetDamageType(AnimationEvent e)
    {
        damageType = V_Damage.FormalIntToDamageType(e.intParameter);
        foreach (KeyValuePair<Transform,Decomposition> keyValuePair in bodyPartsHitBoxRegisterDic) 
        {
            if (keyValuePair.Value != null)
            {
                keyValuePair.Value._HitBox.SetDamageType(damageType);
            }
        }
    }
        
    public void SetAllBodyMarkerManagersIn()
    {
        bodyparts = bodyPartsHitBoxRegisterDic.Keys.ToList();
        for (int i = 0; i < bodyparts.Count;i++)
        {
            if (bodyparts[i] !=null)
            {
                hiddenMethods.RegisterBodyPartWeapon(bodyparts[i],1);
            }
        }
    }

    public void SetRightHandMarkerManager (int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(right_hand,in_or_out);
    }
    
    public void SetLeftHandMarkerManager(int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(left_hand,in_or_out);
    }
    
    public void SetLeftFootMarkerManager(int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(left_foot,in_or_out);
    }
    
    public void SetRightFootMarkerManager(int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(right_foot,in_or_out);
    }
    
    public void SetHeadMarkerManager(int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(head,in_or_out);
    }
    
    public void SetTailMarkerManager(int in_or_out = 1)
    {
        hiddenMethods.RegisterBodyPartWeapon(tail,in_or_out);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

public partial class BO_Ani_E : MonoBehaviour
{
    public HiddenMethods hiddenMethods;
    public Data_Center _DATA_CENTER;
    string magic_path;
    Transform right_hand, left_hand, right_foot, left_foot, head, tail;
    DecompositionPool target_pool;
    readonly IDictionary<Transform, Decomposition> EffectsOnBodyParts = new Dictionary<Transform, Decomposition>();
    readonly List<Decomposition> OnProcessEnergyFromBodyWeapons = new List<Decomposition>();
    
    Decomposition processingHitBox;
    
    void Awake()
    {
        hiddenMethods = new HiddenMethods(this);
    }

    void Start()
    {
        hiddenMethods.SetBodyPartsTransform();// 设置为private目的是减少出现在inpector里的函数数量
    }
    
    public void CloseOnProcessEnergyFromBodyWeapons()
    {
        for (var i = 0; i < OnProcessEnergyFromBodyWeapons.Count; i++)
        {
            OnProcessEnergyFromBodyWeapons[i].Phase = -1;
        }
    }
    
    // 这个系列的函数现在也有对重要变量myMagicForwardPath赋值的作用,所以不可以放在defaultPool里去
    // 另外这个系列的函数经常因为一些初始化流程问题忽略，它必须在模型起到展示技能或实际战斗之前执行，否则找不到特效
    public void BasicMagicAndEffectsPathDefine(Element element)
    {
        magic_path = FightGlobalSetting.EffectPathDefine(element);
    }
    
    void DecideTarget(string bodyPartName)
    {
        switch (bodyPartName)
        {
            case "right_hand":
                target = right_hand;
                break;
            case "left_hand":
                target = left_hand;
                break;
            case "right_foot":
                target = right_foot;
                break;
            case "left_foot":
                target = left_foot;
                break;
            case "head":
                target = head;
                break;
            case "tail":
                target = tail;
                break;
            case "center":
                target = _DATA_CENTER.geometryCenter;
                break;
            default:
                target = _DATA_CENTER.WholeT;
                break;
        }
    }

    AudioClip audioClip;
    public void PlaySoundOnce(string soundClipName)
	{
        AudioResourceLoading.Instance.soundClipsDic.TryGetValue("Audios/effects/" + soundClipName, out audioClip);
        if (audioClip != null)
            _DATA_CENTER._AudioSource.PlayOneShot(audioClip);
	}
    
    public void MagicForward(AnimationEvent e)
    {
        this.hiddenMethods.MagicForward_core(
            e.stringParameter,
            _DATA_CENTER.geometryCenter.position + transform.forward * e.floatParameter,
            transform.rotation,
            e.intParameter,
            null);
    }
    
    public void MagicForwardOnBody(AnimationEvent e)
    {
        this.hiddenMethods.MagicForward_core(
            e.stringParameter,
            _DATA_CENTER.geometryCenter.position + transform.forward * e.floatParameter,
            transform.rotation,
            e.intParameter,
            null, 
            false);
    }

    public void MagicToEnemy(AnimationEvent e)
    {
        Vector3 nav_target;
        if (_DATA_CENTER.Sensor.GetClosestEnemyColliderInSensorRange() != null)
        {
            nav_target = _DATA_CENTER.Sensor.GetClosestEnemyColliderInSensorRange().transform.position;
        }
        else if (_DATA_CENTER.Sensor.GetEnemiesByDistance(false).Count > 0)
        {
            nav_target = _DATA_CENTER.Sensor.GetEnemiesByDistance(false)[0].transform.position;
        }
        else
        {
            nav_target = _DATA_CENTER.geometryCenter.position + transform.forward * 5f;
        }
        
        this.hiddenMethods.MagicForward_core(
            e.stringParameter,
            nav_target,
            transform.rotation,
            e.intParameter,
            null);
    }
    
    public void ReleasePreparedMagic(AnimationEvent e)
    {
        DecideTarget(e.stringParameter);
        hiddenMethods.ReleasePreparedMagic_core(target.position, transform.rotation, target, e.floatParameter, null);
    }
    
    public void ReleasePreparedMagicToAir(AnimationEvent e)
    {
        DecideTarget(e.stringParameter);
        hiddenMethods.ReleasePreparedMagic_core(target.position, transform.rotation, null, e.floatParameter, null);
    }
    
    public void ReleasePreparedEffect(AnimationEvent e)
    {
        DecideTarget(e.stringParameter);
        hiddenMethods.ReleasePreparedEffect_core(target.position, transform.rotation, target);
    }
    
    public void ReleasePreparedEffectToAir(AnimationEvent e)
    {
        DecideTarget(e.stringParameter);
        hiddenMethods.ReleasePreparedEffect_core(target.position, transform.rotation, null);
    }

    Vector3 intPos;
    public void Bullet_shoot_from_body_part(AnimationEvent e)
	{
        DecideTarget(e.stringParameter);
        hiddenMethods.Bullet_shoot_from_Core(target.position, transform.rotation,e.intParameter, e.floatParameter,null);
    }
    
    Transform target;
    Decomposition effect;
    ConstraintSource myConstraintSource;
    public async void EffectOnBodyPart(AnimationEvent e)
	{
        DecideTarget(e.stringParameter);
        effect = await EffectsManager.GenerateEffect("normal_effect", magic_path, target.position, target.rotation,target);
        
        // 我真是不敢相信我们曾经把问题考虑的那么复杂
		// switch (e.intParameter) 
		// {
		// 	case 3:
  //               effect = await EffectsManager.GenerateEffect("long_effect", magic_path, target.position, target.rotation,target);
		// 	    break;
		// 	case 1:
  //               effect = await EffectsManager.GenerateEffect("short_effect", magic_path, target.position, target.rotation,target);
  //               break;
		// 	case 2:
  //               effect = await EffectsManager.GenerateEffect("normal_effect", magic_path, target.position, target.rotation,target);
  //               break;
		// 	default:
  //               effect = await EffectsManager.GenerateEffect("short_effect", magic_path, target.position, target.rotation,target);
  //               break;
		// }
           
        if (EffectsOnBodyParts.ContainsKey(target))
        {
            if (EffectsOnBodyParts[target] != null)
            {
                EffectsOnBodyParts[target].StopEmissions(true);
                EffectsOnBodyParts[target].GetPositionConstraint().constraintActive = false;
            }
            EffectsOnBodyParts[target] = effect; 
        }
	}

    public void BlastAttack(AnimationEvent e)
	{
        DecideTarget(e.stringParameter);
        hiddenMethods.BlastAttack_core(target.position,target.rotation,target,e.intParameter,null);//这部分函数直接让processingHitBox等于刚rent出来的物件，所以接下来可以直接用processingHitBox
    }
    
    string OnLoadMagic;
    void PrepareOneMagic(string magicname)
    {
        OnLoadMagic = magicname;
    }

    string OnLoadEffect;
    void PrepareOneEffect(string magicname)
    {
        OnLoadEffect = magicname;
    }

    //-1 后退
    void Flash(int mode)
    {
        float dis_from_center;
        switch (mode)
        {
            case -1:
                hiddenMethods.Flash(targetPos(-transform.forward, 5f));
                break;
            case 1:
                hiddenMethods.Flash(targetPos(transform.forward, 5f));
                break;
            case 2:
                hiddenMethods.Flash(targetPos(transform.forward, 8f));
                break;
        }

        Vector3 targetPos(Vector3 direction, float step)
        {
            Vector3 temp = transform.position + direction * step;
            if (FightGlobalSetting.SceneStep == 1)
            {
                temp.y = 0;
                dis_from_center = temp.magnitude;
                if (dis_from_center > BoundaryControlByGod._BattleRingRadius)
                {
                    temp = temp.normalized * BoundaryControlByGod._BattleRingRadius;
                }
            }
            return temp;
        }
    }
    
    //-1 后退
    void Rush(int mode)
    {
        float dis_from_center;
        switch (mode)
        {
            case -1:
                hiddenMethods.Flash(targetPos(-transform.forward, 5f));
                break;
            case 1:
                hiddenMethods.Flash(targetPos(transform.forward, 5f));
                break;
            case 2:
                hiddenMethods.Flash(targetPos(transform.forward, 8f));
                break;
        }
        
        Vector3 targetPos(Vector3 direction, float step)
        {
            _DATA_CENTER.Sensor.DetectionStart(5, true);
            Collider collider = _DATA_CENTER.Sensor.GetClosestEnemyColliderInSensorRange();
            Vector3 temp;
            if (collider == null)
            {
                temp = transform.position + direction * step;
            }else{
                temp = collider.transform.position;
                temp.y = 0;
                float tome = Vector3.Distance(transform.position, temp);
                if (tome < step)
                {
                    direction = (temp - transform.position).normalized;
                    temp = transform.position + direction * (tome - 1);
                }else{
                    temp = transform.position + direction * step;
                }
            }
            
            if (FightGlobalSetting.SceneStep == 1)
            {
                temp.y = 0;
                dis_from_center = temp.magnitude;
                if (dis_from_center > BoundaryControlByGod._BattleRingRadius)
                {
                    temp = temp.normalized * BoundaryControlByGod._BattleRingRadius;
                }
            }
            return temp;
        }
    }
}

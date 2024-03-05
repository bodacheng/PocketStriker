using UniRx;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ResistanceManager : MonoBehaviour
{
    public Data_Center data_Center;
    SingleAssignmentDisposable _resistColorChange;
    
    void Awake()
    {
        OpenResistRender();
    }
    
    void OpenResistRender()
    {
        data_Center.FightDataRef.Resistance.Subscribe(
            x => 
            {
                if (x > 0 && data_Center._ShaderManager.HasDoing()) // 其他染色任务优先
                {
                    return;
                }
                if (x > 0)
                {
                    data_Center._ShaderManager.RimEffectsUp(CommonSetting.ResistColor, 0.2f);
                }
                else
                {
                    data_Center._ShaderManager.RimEffectsClear(0.2f);
                }
            }
        ).AddTo(this.gameObject);
    }

    
    public void ResistanceUp(AnimationEvent R)
    {
        data_Center.FightDataRef.Resistance.Value += R.intParameter;
        switch (R.stringParameter)
        {
            case "resistup":
                UnityEngine.Events.UnityAction eventStart = () =>
                {
                    data_Center.FightDataRef.Resistance.Value += 1;
                    data_Center._SkillCancelFlag.turn_on_flag();
                    EffectsManager.GenerateEffect(CommonSetting.BreakFreeEffectCode, 
                        FightGlobalSetting.EffectPathDefine(), data_Center.geometryCenter.position, data_Center.geometryCenter.rotation, data_Center.geometryCenter).Forget();
                };
                UnityEngine.Events.UnityAction eventEnd = () =>
                {
                    data_Center.FightDataRef.Resistance.Value -= 1;
                    data_Center.FightDataRef.RemoveEventKey("resistup");
                };
                CustomCoroutine eventCoroutine = new CustomCoroutine(eventStart, 0.8f, 
                () => data_Center._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.Hit, eventEnd);
                
                data_Center.FightDataRef.AddGetHitTriggerEvent("resistup", () =>
                {
                    data_Center.buffsRunner.RunSubCoroutineOfState(eventCoroutine);
                });
                break;
            case "speedup":
                UnityEngine.Events.UnityAction eventStart3 = () =>
                {
                    data_Center._SkillCancelFlag.turn_on_flag();
                    data_Center.FightDataRef.Resistance.Value += 1;
                    data_Center._ShaderManager.RimEffectsUp(CommonSetting.SpeedColor, 0.2f);
                    data_Center.AnimationManger.AddSpeedBuff("speedup", 2);
                    EffectsManager.GenerateEffect("speedupbuff", FightGlobalSetting.EffectPathDefine(), data_Center.WholeT.position, data_Center.WholeT.rotation, data_Center.WholeT).Forget();
                };
                UnityEngine.Events.UnityAction eventEnd3 = () =>
                {
                    data_Center.AnimationManger.RemoveSpeedBuff("speedup");
                    data_Center.FightDataRef.RemoveEventKey("speedup");
                    data_Center.FightDataRef.Resistance.Value -= 1;
                    data_Center._ShaderManager.RimEffectsClear(0.2f);
                };
                
                CustomCoroutine eventCoroutine3 = new CustomCoroutine(
                    eventStart3, 0.8f,
                     () => data_Center._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.Hit, eventEnd3);
                
                data_Center.FightDataRef.AddGetHitTriggerEvent("speedup", () =>
                {
                    data_Center.buffsRunner.RunSubCoroutineOfState(eventCoroutine3);
                });
                break;
            case "magic_release":
                UnityEngine.Events.UnityAction eventStart2 = () =>
                {
                    data_Center._BO_Ani_E.hiddenMethods.ReleasePreparedMagic_core(transform.position,transform.rotation, null, 1, data_Center._MyBehaviorRunner.GetNowState().StateKey);
                };
                UnityEngine.Events.UnityAction eventEnd2 = () =>
                {
                    data_Center.FightDataRef.RemoveEventKey("magic_release");
                    data_Center._SkillCancelFlag.turn_on_flag();
                    data_Center.FightDataRef.Resistance.Value = 0;
                };
                var eventCoroutine2 = new CustomCoroutine(eventStart2, 0.2f, eventEnd2);
                
                data_Center.FightDataRef.AddGetHitTriggerEvent("magic_release", () =>
                {
                    data_Center.buffsRunner.RunSubCoroutineOfState(eventCoroutine2);
                });
                break;
        }
    }
    
    // 这里面建立在一个前提上是我们认为这个游戏里所有防反技能的吸收伤害期间结束时都同时会Resistance.Value = 0.
    public void ResistanceClear()
    {
        data_Center.FightDataRef.Resistance.Value = 0;
        data_Center.FightDataRef.GetHitTriggerEvents.Clear();
    }
}
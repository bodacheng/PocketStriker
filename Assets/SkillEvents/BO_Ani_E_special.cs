using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HittingDetection;
using UnityEngine.Animations;
using UniRx;
using Log;

public partial class BO_Ani_E : MonoBehaviour
{
    public class HiddenMethods
    {
        readonly BO_Ani_E Ani_E;
        public HiddenMethods(BO_Ani_E bae)
        {
            Ani_E = bae;
        }
        
        public void SetBodyPartsTransform()
        {
            if (Ani_E._DATA_CENTER != null)
            {
                if (Ani_E._DATA_CENTER.right_hand_t != null)
                {
                    Ani_E.right_hand = Ani_E._DATA_CENTER.right_hand_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.right_hand,null);
                }
                if (Ani_E._DATA_CENTER.left_hand_t != null)
                {
                    Ani_E.left_hand = Ani_E._DATA_CENTER.left_hand_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.left_hand,null);
                }
                if (Ani_E._DATA_CENTER.right_foot_t != null)
                {
                    Ani_E.right_foot = Ani_E._DATA_CENTER.right_foot_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.right_foot,null);
                }
                if (Ani_E._DATA_CENTER.left_foot_t != null)
                {
                    Ani_E.left_foot = Ani_E._DATA_CENTER.left_foot_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.left_foot,null);
                }
                if (Ani_E._DATA_CENTER.head_t != null)
                {
                    Ani_E.head = Ani_E._DATA_CENTER.head_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.head,null);
                }
                if (Ani_E._DATA_CENTER.tail_t != null)
                {
                    Ani_E.tail = Ani_E._DATA_CENTER.tail_t.transform;
                    Ani_E.EffectsOnBodyParts.Add(Ani_E.tail,null);
                }
            }
        }
        
        public void AddOnProcessEnergyFromBodyWeapons(Decomposition decomposition)
        {
            Ani_E.OnProcessEnergyFromBodyWeapons.Add(decomposition);
            SingleAssignmentDisposable Disposable = new SingleAssignmentDisposable();
                Disposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (decomposition.Phase == 0 || decomposition.Phase == -1)
                    {
                        Ani_E.OnProcessEnergyFromBodyWeapons.Remove(decomposition);
                        Disposable.Dispose();
                    }
                }
            );
            SingleAssignmentDisposableCleaner.Add(Disposable);
        }
        
        public void BlastAttack_core(Vector3 pos, Quaternion qua , Transform parentTarget, int grade, string logForStateKey)
        {
            switch (grade)
            {
                case 0:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("blast", Ani_E.magic_path);
                    break;
                case 1:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("blast", Ani_E.magic_path);
                    break;
                case 2:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("big_blast", Ani_E.magic_path);
                    break;
                default:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("blast", Ani_E.magic_path);
                    break;
            }
            
            Ani_E.processingHitBox = Ani_E.target_pool.Rent();
            Ani_E.processingHitBox._HitBox.SetOwnerFACR(Ani_E._DATA_CENTER.FightDataRef);
            Ani_E.processingHitBox.transform.position = pos;
            Ani_E.processingHitBox.transform.rotation = qua;
            Ani_E.processingHitBox._HitBox._WeaponMode = WeaponMode.EnergyFromBodyWeapon;
            if (parentTarget != null)
            {
                Ani_E.myConstraintSource.sourceTransform = parentTarget;
                Ani_E.myConstraintSource.weight = 1;
                Ani_E.processingHitBox.GetPositionConstraint().SetSources(new List<ConstraintSource>{Ani_E.myConstraintSource});
                Ani_E.processingHitBox.GetPositionConstraint().constraintActive = true;
                Ani_E.processingHitBox.GetPositionConstraint().locked = true;
                Ani_E.processingHitBox.GetPositionConstraint().translationOffset = Vector3.zero;
            }else{
                Ani_E.processingHitBox.GetPositionConstraint().constraintActive = false;
            }
            Ani_E.processingHitBox.SetBOAniE(Ani_E);
            if (Ani_E.processingHitBox.TrackControl != null && Ani_E.processingHitBox.TrackControl._TrackMode == TrackControl.TrackMode.Navigation)
            {
                Ani_E.processingHitBox.TrackControl.Sensor = Ani_E._DATA_CENTER.Sensor;
            }
            Ani_E.processingHitBox._HitBox.SetReferenceTransformInfo(Ani_E._DATA_CENTER.geometryCenter);
            if (Ani_E._DATA_CENTER._TeamConfig != null)
            {
                Ani_E.processingHitBox._HitBox.SetTeamConfig(Ani_E._DATA_CENTER._TeamConfig);
                Ani_E.processingHitBox._HitBox.MarkersEnablingStarts();
            }
            
            if (FightGlobalSetting.HitBoxLogger)
            {
                Ani_E.processingHitBox._HitBox.GeneratedByStateKey = logForStateKey ?? Ani_E._DATA_CENTER._MyBehaviorRunner.GetNowState().StateKey;
                Ani_E.processingHitBox._HitBox.HitBoxLifeEnding = HitBoxLifeEnding.untouched;
            }
        }
        
        public void Bullet_shoot_from_Core(Vector3 pos, Quaternion qua, int grade, float speed, string logForStateKey)
        {
            switch (grade)
            {
                case 1:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("bullet", Ani_E.magic_path);
                    break;
                case 2:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("big_bullet", Ani_E.magic_path);
                    break;
                case 3:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("super_bullet", Ani_E.magic_path);
                    break;
                default:
                    Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool("bullet", Ani_E.magic_path);
                    break;
            }

            if (Ani_E.target_pool == null)
            {
                Debug.Log("基本逻辑错误："+grade);
                return;
            }
            
            Ani_E.processingHitBox = Ani_E.target_pool.Rent();
            Ani_E.processingHitBox._HitBox.SetOwnerFACR(Ani_E._DATA_CENTER.FightDataRef);
            Ani_E.processingHitBox.transform.position = pos;
            Ani_E.processingHitBox.transform.rotation = qua;
            EffectsManager.GenerateEffect(Ani_E.processingHitBox._HitBox.muzzle, Ani_E.magic_path, Ani_E.processingHitBox.transform.position, Ani_E.transform.rotation, null).Forget();
            Ani_E.processingHitBox._HitBox.SetReferenceTransformInfo(Ani_E.processingHitBox.transform);
            Ani_E.processingHitBox._HitBox._WeaponMode = WeaponMode.FlyerWeapon;
            Ani_E.processingHitBox.SetBOAniE(Ani_E);
            if (Ani_E.processingHitBox.TrackControl != null && Ani_E.processingHitBox.TrackControl._TrackMode == TrackControl.TrackMode.Navigation)
            {
                Ani_E.processingHitBox.TrackControl.Sensor = Ani_E._DATA_CENTER.Sensor;
            }
            if (Ani_E._DATA_CENTER._TeamConfig != null)
            {
                Ani_E.processingHitBox._HitBox.SetTeamConfig(Ani_E._DATA_CENTER._TeamConfig);
                Ani_E.processingHitBox._HitBox.MarkersEnablingStarts();
            }
            if (Ani_E.processingHitBox.TrackControl != null)
            {
                Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position, Ani_E.processingHitBox.transform.rotation, speed);
            }
            
            if (FightGlobalSetting.HitBoxLogger)
            {
                Ani_E.processingHitBox._HitBox.GeneratedByStateKey = logForStateKey ?? Ani_E._DATA_CENTER._MyBehaviorRunner.GetNowState().StateKey;
                Ani_E.processingHitBox._HitBox.HitBoxLifeEnding = HitBoxLifeEnding.untouched;
            }
        }
        
        public void MagicForward_core(string objectName, Vector3 pos, Quaternion qua, int speedGrade, string logForStateKey, bool asFlyerWeapon = true)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }
            
            Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool(objectName, Ani_E.magic_path);
            if (Ani_E.target_pool != null)
            {
                Ani_E.processingHitBox = Ani_E.target_pool.Rent();
                Ani_E.processingHitBox._HitBox.SetOwnerFACR(Ani_E._DATA_CENTER.FightDataRef);
                Ani_E.processingHitBox.transform.position = pos;
                Ani_E.processingHitBox.transform.rotation = qua;
                Ani_E.processingHitBox._HitBox.SetReferenceTransformInfo(Ani_E.processingHitBox.transform);
                Ani_E.processingHitBox._HitBox._WeaponMode = asFlyerWeapon ? WeaponMode.FlyerWeapon : WeaponMode.EnergyFromBodyWeapon;
                Ani_E.processingHitBox.SetBOAniE(Ani_E);
                if (Ani_E.processingHitBox.TrackControl != null && Ani_E.processingHitBox.TrackControl._TrackMode == TrackControl.TrackMode.Navigation)
                {
                    Ani_E.processingHitBox.TrackControl.Sensor = Ani_E._DATA_CENTER.Sensor;
                }
                Ani_E.processingHitBox._HitBox.SetTeamConfig(Ani_E._DATA_CENTER._TeamConfig);
                Ani_E.processingHitBox._HitBox.MarkersEnablingStarts();
                if (Ani_E.processingHitBox._HitBox.onGroundMagic)
                {
                    Ani_E.processingHitBox.transform.position = new Vector3(Ani_E.processingHitBox.transform.position.x, Ani_E.transform.position.y, Ani_E.processingHitBox.transform.position.z);
                }
                if (Ani_E.processingHitBox.TrackControl != null)
                {
                    switch (speedGrade)
                    {
                        case 1:
                            Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position,qua, 1f);
                            break;
                        case 2:
                            Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position,qua, 3f);
                            break;
                        case 3:
                            Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position,qua, 5f);
                            break;
                        case 0:
                            Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position,qua, 0f);
                            break;
                        default:
                            Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position,qua, 0f);
                            break;
                    }
                }
                
                if (FightGlobalSetting.HitBoxLogger)
                {
                    Ani_E.processingHitBox._HitBox.GeneratedByStateKey = logForStateKey ?? Ani_E._DATA_CENTER._MyBehaviorRunner.GetNowState().StateKey;
                    Ani_E.processingHitBox._HitBox.HitBoxLifeEnding = HitBoxLifeEnding.untouched;
                }
            }
        }
        
        public void ReleasePreparedMagic_core(Vector3 pos, Quaternion qua, Transform parentT, float trackSpeed, string logForStateKey)
        {
            if (Ani_E.OnLoadMagic == null)
                return;
            Ani_E.target_pool = HurtObjectManager.GetHurtObjectPool(Ani_E.OnLoadMagic, Ani_E.magic_path);
            if (Ani_E.target_pool == null)
                return;
        
            Ani_E.processingHitBox = Ani_E.target_pool.Rent();
            Ani_E.processingHitBox._HitBox.SetOwnerFACR(Ani_E._DATA_CENTER.FightDataRef);
            Ani_E.processingHitBox.transform.position = pos;
            if (Ani_E.processingHitBox._HitBox.onGroundMagic)
            {
                Ani_E.processingHitBox.transform.position = new Vector3(Ani_E.processingHitBox.transform.position.x, Ani_E.transform.position.y, Ani_E.processingHitBox.transform.position.z);
            }
            Ani_E.processingHitBox.transform.rotation = qua;
            if (parentT != null)
            {
                Ani_E.myConstraintSource.sourceTransform = parentT;
                Ani_E.myConstraintSource.weight = 1;
                Ani_E.processingHitBox.GetPositionConstraint().SetSources(new List<ConstraintSource>{Ani_E.myConstraintSource});
                Ani_E.processingHitBox.GetPositionConstraint().constraintActive = true;
                Ani_E.processingHitBox.GetPositionConstraint().locked = true;
            }else{
                Ani_E.processingHitBox.GetPositionConstraint().constraintActive = false;
            }
            Ani_E.processingHitBox.SetBOAniE(Ani_E);
            if (Ani_E.processingHitBox._HitBox._WeaponMode == WeaponMode.EnergyFromBodyWeapon)
            {
                AddOnProcessEnergyFromBodyWeapons(Ani_E.processingHitBox);
            }
            if (Ani_E.processingHitBox.TrackControl != null && Ani_E.processingHitBox.TrackControl._TrackMode == TrackControl.TrackMode.Navigation)
            {
                Ani_E.processingHitBox.TrackControl.Sensor = Ani_E._DATA_CENTER.Sensor;
            }
            Ani_E.processingHitBox._HitBox.SetReferenceTransformInfo(Ani_E._DATA_CENTER.geometryCenter);
            Ani_E.processingHitBox._HitBox.SetTeamConfig(Ani_E._DATA_CENTER._TeamConfig);
            Ani_E.processingHitBox._HitBox.MarkersEnablingStarts();
            if (Ani_E.processingHitBox.TrackControl != null)
            {
                Ani_E.processingHitBox.TrackControl.StartOff(Ani_E.processingHitBox.transform.position, qua, trackSpeed);
            }
            
            if (FightGlobalSetting.HitBoxLogger)
            {
                Ani_E.processingHitBox._HitBox.GeneratedByStateKey = logForStateKey ?? Ani_E._DATA_CENTER._MyBehaviorRunner.GetNowState().StateKey;
                Ani_E.processingHitBox._HitBox.HitBoxLifeEnding = HitBoxLifeEnding.untouched;
            }
        }

        public void ReleasePreparedEffect_core(Vector3 pos, Quaternion qua, Transform parentT)
        {
            if (Ani_E.OnLoadEffect == null)
                return;
            EffectsManager.GenerateEffect(Ani_E.OnLoadEffect, Ani_E.magic_path , pos, qua, parentT).Forget();
        }

        public void CloseEffectsOnBodyParts(bool clearParticles)
        {
            foreach (KeyValuePair<Transform, Decomposition> keyValuePair in Ani_E.EffectsOnBodyParts)
            {
                if (keyValuePair.Value != null)
                {
                    keyValuePair.Value.StopEmissions(clearParticles);
                }
            }
        }

        public void Flash(Vector3 targetpos)
        {
            Vector3 StartToEnd = targetpos - Ani_E.transform.position;
            StartToEnd.y = 0;
            EffectsManager.GenerateEffect("FlashStart", Ani_E.magic_path, Ani_E._DATA_CENTER.geometryCenter.position, Quaternion.LookRotation(StartToEnd, Vector3.up), null).Forget();
            Ani_E.transform.position = targetpos;
            EffectsManager.GenerateEffect("FlashEnd", Ani_E.magic_path, Ani_E._DATA_CENTER.geometryCenter.position, Quaternion.LookRotation(-StartToEnd, Vector3.up), null).Forget();
        }
    }
}
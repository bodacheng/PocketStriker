using UnityEngine;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        UnityEngine.Events.UnityAction pasuestart;
        UnityEngine.Events.UnityAction pasueend;
        CustomCoroutine pasueCoroutine;
        readonly Color gold = new Color(1f, 1, 0.2f);
        
        void TimePauseStart()
        {
            pasuestart = () =>
            {
                _BuffsRunner.Freezing = true;
                AnimationManger.AddSpeedBuff("pasue", 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                shaderManager.FlatColor(gold, 0.5f);
            };
            pasueend = () =>
            {
                AnimationManger.RemoveSpeedBuff("pasue");
                shaderManager.FlatColor(Color.white, 0);
                _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                _BuffsRunner.Freezing = false;
            };
            pasueCoroutine = new CustomCoroutine(pasuestart, FightGlobalSetting.SuperHitLastingTime * 3, pasueend);
            _BuffsRunner.RunSubCoroutineOfState(pasueCoroutine);
        }
    }
}
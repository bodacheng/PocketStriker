//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class Animation_Manger_old : MonoBehaviour
//{

//    // 这是我们这次对系统改修大概最麻烦的一个地方，上次的系统在这个环节可谓是经历了很多麻烦，因此现在我们需要新系统。
//    // 大体的image就是能够直接通过动画文件名来随时决定animator当中各个层动画的播放。而不再需要复杂的animator。。
//    // 想想看这对我们这样想用unity做动画的人来说是种必然的需求。然而这和unity的这个animator系统的初衷产生矛盾
//    //整个系统建立在animator本身这样的前提下：
//    //1. animator在状态迁移过程中，“当前状态”为迁移出发状态
//    //2. 在迁移过程中，如果激发了迁移终点状态向另一状态（或返回至出发状态）迁移的条件，（起码我们知道trigger满足这点），则状态机会在按原节奏到达终点状态后，再去向第三者状态迁移，之前的迁移过程并不会受干扰，后触发的迁移条件也并不会被遗忘，一切会按顺序进行
//    //从而也就是说对最后要触发那个状态来说，从条件激活到开始进入会发生一点延迟。

//    public string clip_path;
//    public Animator Animator;
//    string fullbodylayer_return_trigger_name = null;
//    public AnimationClip current_animation;
//    private string nextStateName;

//    void Awake()
//    {
//        if (Animator == null)
//            Animator = gameObject.GetComponent<Animator>();
//        if (Animator == null)
//            Debug.Log("animator not found");
//    }

//    void Start()
//    {
//    }

//    void Update()
//    {
//        clean_null_state1_and_state2();

//        AnimatorStateInfo AnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo((int)animator_layer_index.Full_Body);
//        bool ifTransition = false;
//        if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.null -> Full Body.full_body_state1")
//            ||Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state1 -> Full Body.null")
//            || Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state2 -> Full Body.null")
//           )
//        {
//            ifTransition = true;
//        }

//        if (current_animation != null)
//        {
//            if (AnimatorStateInfo.IsName("Full Body.null") && !ifTransition)
//            {
//                this.PlayLayerAnim(animator_layer_index.Full_Body, current_animation.name);
//            }
//        }
//    }

//    private void clean_null_state1_and_state2()
//    {
//        AnimatorStateInfo AnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo((int)animator_layer_index.Full_Body);
//        if (AnimatorStateInfo.IsName("Full Body.full_body_state1"))
//        {
//            if (!Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state1 -> Full Body.full_body_state2")
//                &&
//                !Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state1 -> Full Body.null")
//               )
//            {
//                if (Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index.Full_Body)[0].clip.name == "fullbody_empty1")
//                {
//                    Animator.SetTrigger("fullbody_return1");
//                }
//            }
//        }
//        if (AnimatorStateInfo.IsName("Full Body.full_body_state2"))
//        {
//            if (!Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state2 -> Full Body.full_body_state1")
//                &&
//                !Animator.GetAnimatorTransitionInfo((int)animator_layer_index.Full_Body).IsName("Full Body.full_body_state2 -> Full Body.null")
//               )
//            {
//                if (Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index.Full_Body)[0].clip.name == "fullbody_empty2")
//                {
//                    Animator.SetTrigger("fullbody_return2");
//                }
//            }
//        }
//    }

//    public float getCurrentAnimatorStateNormalizedTime(animator_layer_index animator_layer_index)
//    {
//        return Animator.GetCurrentAnimatorStateInfo((int)animator_layer_index).normalizedTime;
//    }

//    public AnimationClip tryAnimationClip(string clip_name)
//    {
//        return Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//    }

//    string to_be_override_animation_name = null;
//    string trigger_name = null;
//    string pre_overrided_anim_name = null;
//    string current_anim_name = null;

//    public void PlayLayerAnim(animator_layer_index animator_layer_index, string clip_name)
//    {
//        AnimatorStateInfo AnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo((int)animator_layer_index);

//        to_be_override_animation_name = null;
//        trigger_name = null;
//        fullbodylayer_return_trigger_name = null;
//        pre_overrided_anim_name = null;
//        current_anim_name = null;

//        switch (animator_layer_index)
//        {
//            case animator_layer_index.Full_Body:
//                if (AnimatorStateInfo.IsName("Full Body.null"))
//                {
//                    if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index).IsName("Full Body.null -> Full Body.full_body_state1"))
//                    {                        
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            nextStateName = "Full Body.full_body_state1 -> Full Body.full_body_state2";
//                            to_be_override_animation_name = "fullbody_empty2";
//                            pre_overrided_anim_name = "fullbody_empty1";
//                            trigger_name = "fullbody_trigger2";
//                            fullbodylayer_return_trigger_name = "fullbody_return1";
//                            the_trigger(clip_name);
//                            break;
//                        }
//                        else if (clip_name == "" || clip_name == null)
//                        {
//                            nextStateName = "Full Body.full_body_state1 -> Full Body.null";
//                            to_be_override_animation_name = null;
//                            pre_overrided_anim_name = "fullbody_empty1";
//                            trigger_name = null;
//                            fullbodylayer_return_trigger_name = "fullbody_return1";
//                            the_trigger(clip_name);
//                            break;
//                        }
//                    }
//                    else
//                    {
//                        nextStateName = "Full Body.null -> Full Body.full_body_state1";
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            to_be_override_animation_name = "fullbody_empty1";
//                            pre_overrided_anim_name = null;
//                            trigger_name = "fullbody_trigger1";
//                            fullbodylayer_return_trigger_name = null;
//                            the_trigger(clip_name);
//                            break;
//                        }
//                        else
//                        {
//                            break;
//                        }
//                    }
//                }
//                if (AnimatorStateInfo.IsName("Full Body.full_body_state1"))
//                {
//                    if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index).IsName("Full Body.full_body_state1 -> Full Body.full_body_state2"))
//                    {                        
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            nextStateName = "Full Body.full_body_state2 -> Full Body.full_body_state1";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = "fullbody_empty1";
//                            pre_overrided_anim_name = "fullbody_empty2";
//                            trigger_name = "fullbody_trigger1";
//                            fullbodylayer_return_trigger_name = "fullbody_return2";
//                            the_trigger(clip_name);
//                        }
//                        else
//                        {
//                            nextStateName = "Full Body.full_body_state2 -> Full Body.null";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = null;
//                            pre_overrided_anim_name = "fullbody_empty2";
//                            trigger_name = null;
//                            fullbodylayer_return_trigger_name = "fullbody_return2";
//                            the_trigger(clip_name);
//                            break;
//                        }
//                    }
//                    else if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index).IsName("Full Body.full_body_state1 -> Full Body.null"))
//                    {                        
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            nextStateName = "Full Body.null -> Full Body.full_body_state1";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = "fullbody_empty1";
//                            pre_overrided_anim_name = null;
//                            trigger_name = "fullbody_trigger1";
//                            fullbodylayer_return_trigger_name = null;
//                            the_trigger(clip_name);
//                            break;
//                        }
//                        else
//                        {
//                            break;
//                        }
//                    }
//                    else
//                    {
//                        nextStateName = "Full Body.full_body_state1 -> Full Body.full_body_state2";
//                        current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                        to_be_override_animation_name = "fullbody_empty2";
//                        pre_overrided_anim_name = "fullbody_empty1";
//                        trigger_name = "fullbody_trigger2";
//                        fullbodylayer_return_trigger_name = "fullbody_return1";
//                        the_trigger(clip_name);
//                        break;
//                    }
//                }
//                if (AnimatorStateInfo.IsName("Full Body.full_body_state2"))
//                {
//                    if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index).IsName("Full Body.full_body_state2 -> Full Body.full_body_state1"))
//                    {
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            nextStateName = "Full Body.full_body_state1 -> Full Body.full_body_state2";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = "fullbody_empty2";
//                            pre_overrided_anim_name = "fullbody_empty1";
//                            trigger_name = "fullbody_trigger2";
//                            fullbodylayer_return_trigger_name = "fullbody_return1";
//                            the_trigger(clip_name);
//                            break;
//                        }
//                        else
//                        {
//                            nextStateName = "Full Body.full_body_state1 -> Full Body.null";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = null;
//                            pre_overrided_anim_name = "fullbody_empty1";
//                            trigger_name = null;
//                            fullbodylayer_return_trigger_name = "fullbody_return1";
//                            the_trigger(clip_name);
//                            break;
//                        }
//                    }
//                    else if (Animator.GetAnimatorTransitionInfo((int)animator_layer_index).IsName("Full Body.full_body_state2 -> Full Body.null"))
//                    {
//                        if (clip_name != "" && clip_name != null)
//                        {
//                            nextStateName = "Full Body.null -> Full Body.full_body_state1";
//                            current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                            to_be_override_animation_name = "fullbody_empty1";
//                            pre_overrided_anim_name = null;
//                            trigger_name = "fullbody_trigger1";
//                            fullbodylayer_return_trigger_name = null;
//                            the_trigger(clip_name);
//                            break;
//                        }
//                        else
//                        {
//                            break;
//                        }
//                    }
//                    else
//                    {
//                        nextStateName = "Full Body.full_body_state2 -> Full Body.full_body_state1";
//                        current_anim_name = Animator.GetCurrentAnimatorClipInfo((int)animator_layer_index)[0].clip.name;
//                        to_be_override_animation_name = "fullbody_empty1";
//                        pre_overrided_anim_name = "fullbody_empty2";
//                        trigger_name = "fullbody_trigger1";
//                        fullbodylayer_return_trigger_name = "fullbody_return2";
//                        the_trigger(clip_name);
//                        break;
//                    }
//                }
//                break;
//            default:
//                break;
//        }
//    }

//    void the_trigger(string clip_name)
//    {
//        RuntimeAnimatorController myController = Animator.runtimeAnimatorController;
//        AnimatorOverrideController myOverrideController = new AnimatorOverrideController(Animator.runtimeAnimatorController);
//        if (myOverrideController != null)
//            myController = myOverrideController.runtimeAnimatorController;

//        AnimatorOverrideController animatorOverride = new AnimatorOverrideController();
//        animatorOverride.runtimeAnimatorController = myController;

//        if (pre_overrided_anim_name != null)
//        {
//            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
//            {
//                animatorOverride[pre_overrided_anim_name] = Resources.Load(clip_path + "/" + current_anim_name) as AnimationClip; // 关于前动画起始点的问题有待商榷。可能没问题。
//            }
//            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
//            {
//                animatorOverride[pre_overrided_anim_name] = Resources.Load(clip_path + "/" + current_anim_name) as AnimationClip; // 关于前动画起始点的问题有待商榷。可能没问题。
//            }
//            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//            {
//                animatorOverride[pre_overrided_anim_name] = Resources.Load(clip_path + "/" + current_anim_name) as AnimationClip;
//            }

//        }
//        else
//        {
//            animatorOverride[pre_overrided_anim_name] = null;//也就是说从null状态出发的时候
//        }

//        if (to_be_override_animation_name != null)
//        {
//            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
//            {
//                animatorOverride[to_be_override_animation_name] = Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//            }
//            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
//            {
//                animatorOverride[to_be_override_animation_name] = Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//            }
//            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//            {
//                animatorOverride[to_be_override_animation_name] = Resources.Load(clip_path + "/" + clip_name) as AnimationClip; // 关于前动画起始点的问题有待商榷。可能没问题。
//            }

//        }
//        Animator.runtimeAnimatorController = animatorOverride;

//        if (clip_name != null && clip_name != "")
//        {
//            if (trigger_name != null)
//            {
//                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
//                {
//                    current_animation = Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//                }
//                if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
//                {
//                    current_animation = Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//                }
//                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
//                {
//                    current_animation = Resources.Load(clip_path + "/" + clip_name) as AnimationClip;
//                }
//                Animator.SetTrigger(trigger_name);

//                if (trigger_name == "fullbody_trigger1" && 
//                    (nextStateName == "Full Body.full_body_state1 -> Full Body.null" || nextStateName == "Full Body.full_body_state1 -> Full Body.full_body_state2" || nextStateName == "Full Body.full_body_state2 -> Full Body.null"))
//                {
//                    Debug.Log("!!!!!!!!!!!!!!");
//                }
//                if (trigger_name == "fullbody_trigger2" && 
//                    (nextStateName == "Full Body.full_body_state2 -> Full Body.null" || nextStateName == "Full Body.full_body_state2 -> Full Body.full_body_state1" || nextStateName == "Full Body.full_body_state2 -> Full Body.full_body_state1"))
//                {
//                    Debug.Log("!??!?!?!?!?!?!?!");
//                }
//                if (nextStateName == "Full Body.full_body_state2 -> Full Body.null" || nextStateName == "Full Body.full_body_state1 -> Full Body.null")
//                {
//                    Debug.Log("卧槽啊！！！！！");
//                }

//                if (animatorOverride[to_be_override_animation_name].name != clip_name)
//                {
//                    Debug.Log(animatorOverride[to_be_override_animation_name].name);
//                }
//            }
//        }
//        else
//        {
//            if (fullbodylayer_return_trigger_name != null)
//            {
//                Animator.SetTrigger(fullbodylayer_return_trigger_name);
//            }
//            current_animation = null;
//        }
//    }

//    public void PlayLayerAnim(animator_layer_index animator_layer_index, string clip_name, float speed)
//    {
//        Animator.speed = speed;
//        PlayLayerAnim(animator_layer_index, clip_name);
//    }
//}

//if (Animator.GetCurrentAnimatorClipInfo(1).Length > 0)
//            {
//                if (Animator.GetCurrentAnimatorClipInfo(1)[0].clip.name != clip_name)
//                {
//                    Debug.Log(Animator.GetCurrentAnimatorClipInfo(1)[0].clip.name);
//                    return false;
//                }
//            }
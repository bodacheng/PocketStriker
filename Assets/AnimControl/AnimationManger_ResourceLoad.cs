using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public partial class AnimationManger
{
    private FacialAnimManager facialAnimManager;

    public void CasualFace()
    {
        if (facialAnimManager != null)
            facialAnimManager.CasualFace();
    }
    
    public void TriggerExpression(Facial facial)
    {
        facialAnimManager?.TriggerExpression(facial);
    }
    
    public async UniTask PreloadBasicPersonalAnims(string type, string basicPackName, FacialAnimManager facialAnimManager = null)
    {
        var basicAnims = new List<AnimationClip>();
        var basicPackKey = type + "/" + basicPackName;
        if (AnimationResourceLoader.SeriesAnimationClipsDic.ContainsKey(basicPackKey))
        {
            AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(basicPackKey, out basicAnims);
        }
        else
        {
            var loadPath = Addressables.LoadResourceLocationsAsync("basic_anim");
            await loadPath;
            if (loadPath.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var path in loadPath.Result)
                {
                    if (path.PrimaryKey.Contains(type + "/BasicPack/" + basicPackName))
                    {
                        Object value = await AddressablesLogic.LoadT<AnimationClip>(path.PrimaryKey);
                        if (value != null)
                        {
                            var animationClip = (AnimationClip)value;
                            basicAnims.Add(animationClip);
                        }
                    }
                }
            }
            Addressables.Release(loadPath);
            DicAdd<string, List<AnimationClip>>.Add(AnimationResourceLoader.SeriesAnimationClipsDic, basicPackKey, basicAnims);
        }

        toLoadAnims = new Dictionary<string, AnimationClip>();
        if (basicAnims != null)
        {
            foreach (var animationClip in basicAnims)
            {
                if (animationClip.name == "death")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("death", animationClip));
                }

                if (animationClip.name == "rush")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("rush", animationClip));
                }

                if (animationClip.name == "block")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("block", animationClip));
                }

                if (animationClip.name == "block_break")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("block_break", animationClip));
                }
                // if (animationClip.name == "dash")
                // {
                //     toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("dash", animationClip));
                // }
                // if (animationClip.name == "rushback")
                // {
                //     toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("rushback", animationClip));
                // }
                
                if (animationClip.name == "getup")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("getup", animationClip));
                }

                if (animationClip.name == "victory")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("victory", animationClip));
                }
            }
        }
        else
        {
            Debug.Log("Basic Anim Pack Error:" + type + "  " + basicPackName);
        }

        async UniTask LoadHurtAnim(string type, string address, List<string> tags)
        {
            var key = type + "/" + address;
            if (!AnimationResourceLoader.SeriesAnimationClipsDic.ContainsKey(key))
            {
                AnimationResourceLoader.SeriesAnimationClipsDic.Add(key, new List<AnimationClip>());
                var humanHurtAnimsObjects = new List<AnimationClip>();
                var loadPath = Addressables.LoadResourceLocationsAsync(tags, Addressables.MergeMode.Intersection);
                await loadPath;
                if (loadPath.Status == AsyncOperationStatus.Succeeded)
                {
                    foreach (var path in loadPath.Result)
                    {
                        if (path.PrimaryKey.Contains(key))
                        {
                            Object value = await AddressablesLogic.LoadT<AnimationClip>(path.PrimaryKey);
                            if (value != null)
                            {
                                var animationClip = (AnimationClip)value;
                                humanHurtAnimsObjects.Add(animationClip);
                            }
                        }
                    }
                }
                
                Addressables.Release(loadPath);
                foreach (var clip in humanHurtAnimsObjects)
                {
                    if (AnimationResourceLoader.SeriesAnimationClipsDic.ContainsKey(key))
                    {
                        AnimationResourceLoader.SeriesAnimationClipsDic[key].Add(clip);
                    }
                    else
                    {
                        Debug.Log(key+ " ： 动画读取逻辑错误");
                        AnimationResourceLoader.SeriesAnimationClipsDic.Add(key, new List<AnimationClip>(){clip});
                    }
                }
            }
        }

        await UniTask.WhenAll(
            LoadHurtAnim(type, "basic_hurts/back", new List<string> { "hurt_anim" }),
            LoadHurtAnim(type, "basic_hurts/high", new List<string> { "hurt_anim" }),
            LoadHurtAnim(type, "basic_hurts/lay", new List<string> { "hurt_anim" }),
            LoadHurtAnim(type, "basic_hurts/low", new List<string> { "hurt_anim" }),
            LoadHurtAnim(type, "basic_hurts/press", new List<string> { "hurt_anim" }),
            LoadHurtAnim(type, "basic_knockoffs", new List<string> { "knock_anim" })
        );
        
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_knockoffs", out knockoffAnimations);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_hurts/back", out _hurtClipsBack);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_hurts/low", out _hurtClipsLow);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_hurts/high", out _hurtClipsHigh);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_hurts/press", out _hurtClipsPress);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(type + "/basic_hurts/lay", out _hurtClipsLay);

        if (Animator == null || Animator.gameObject == null)
        {
            return; // When the character model is displayed, there may be issues such as the menu suddenly closing
        }
        animatorOverride = new AnimatorOverrideController(Animator.runtimeAnimatorController);

        // 以上内容为个性化动画片段对base层基础动画的覆盖
        foreach (var animationClip in basicAnims)
        {
            if (animationClip.name == "idle")
            {
                if (animatorOverride["idle"])
                    animatorOverride["idle"] = animationClip;
            }
            
            if (animationClip.name == "walk")
            {
                if (animatorOverride["walk"])
                    animatorOverride["walk"] = animationClip;
            }
            
            if (animationClip.name == "run")
            {
                if (animatorOverride["run"])
                    animatorOverride["run"] = animationClip;
            }
            
            if (animationClip.name == "air")
            {
                if (animatorOverride["air"])
                    animatorOverride["air"] = animationClip;
            }
        }

        this.facialAnimManager = facialAnimManager;
        this.facialAnimManager?.INI(Animator, animatorOverride);
        
        Animator.runtimeAnimatorController = animatorOverride;
    }
    
    public async UniTask PreloadPersonalAnimResourceMode(string animPath, string key, Element element, int preloadCount)
    {
        if (toLoadAnims.ContainsKey(key))
        {
            return;
        }
        
        await AnimationResourceLoader.LoadAnim(animPath, key);
        var clip = AnimationResourceLoader.Instance.GetAnimationClip(animPath + "/skill/" + key);
        if (clip != null)
        {
            if (!toLoadAnims.ContainsKey(key))
            {
                toLoadAnims.Add(new KeyValuePair<string, AnimationClip>(key, clip));
                var tasks = new List<UniTask>();
                
                foreach (AnimationEvent e in clip.events)
                {
                    if (e.functionName == "MagicForward")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "MagicForwardOnBody")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "MagicToEnemy")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "PrepareOneMagic")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "Bullet_shoot_from_body_part")
                    {
                        switch (e.intParameter)
                        {
                            case 1:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("bullet", element, preloadCount));
                                break;
                            case 2:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("big_bullet", element, preloadCount));
                                break;
                            case 3:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("super_bullet", element, preloadCount));
                                break;
                            default:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("bullet", element, preloadCount));
                                break;
                        }
                    }
                    if (e.functionName == "BlastAttack")
                    {
                        switch (e.intParameter)
                        {
                            case 0:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, 1));
                                break;
                            case 1:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, 1));
                                break;
                            case 2:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("big_blast", element, 1));
                                break;
                            default:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, 1));
                                break;
                        }
                    }
                    if (e.functionName == "PlaySoundOnce")
                    {
                        AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic("effects", e.stringParameter);
                    }
                }
                await UniTask.WhenAll(tasks);
            }
        }
    }
    
    public async UniTask PreloadPersonalAnimsResourceMode(string type, List<string> toLoadSkillAnimsNames, Element element, int preloadCount)
    {
        var tasks = new List<UniTask>();
        foreach (var animName in toLoadSkillAnimsNames)
        {
            tasks.Add(PreloadPersonalAnimResourceMode(type, animName, element, preloadCount));
        }
        await UniTask.WhenAll(tasks);
    }
}

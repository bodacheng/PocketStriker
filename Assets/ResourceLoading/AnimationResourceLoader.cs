using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AnimationResourceLoader
{
    static AnimationResourceLoader instance;
    public static AnimationResourceLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AnimationResourceLoader();
            }
            return instance;
        }
    }
    
    static readonly IDictionary<string, AnimationClip> AnimationClipDic = new Dictionary<string, AnimationClip>();
    public static readonly IDictionary<string, List<AnimationClip>> SeriesAnimationClipsDic = new Dictionary<string, List<AnimationClip>>();//这个是用来记录那些一个包里好几个动画的
    //public IDictionary<string, RuntimeAnimatorController> RuntimeAnimatorControllerIdic = new Dictionary<string, RuntimeAnimatorController>();

    public void Clear()
    {
        AnimationClipDic.Clear();
        SeriesAnimationClipsDic.Clear();
    }
    
    //战斗场景下角色实时读取动作走的是这个，所以必然的我们有了getAnimationClip和ConstructAnimationClip
    public AnimationClip GetAnimationClip(string key)
    {
        AnimationClipDic.TryGetValue(key, out AnimationClip _AnimationClip);
        return _AnimationClip;
    }

    public static async UniTask LoadAnim(string type, string key)
    {
        var clipKey = type + "/skill/" + key;
        if (AnimationClipDic.ContainsKey(clipKey))
        {
            return;
        }
        
        var result = await AddressablesLogic.LoadT<AnimationClip>(type+"/skill/"+ key +".anim");
        if (result != null)
        {
            DicAdd<string, AnimationClip>.Add(AnimationClipDic, clipKey, result);
        }
    }
}

//public RuntimeAnimatorController getRuntimeAnimatorController(string type)
    //{
    //    RuntimeAnimatorController toLoadRuntimeAnimatorController;
    //    RuntimeAnimatorControllerIdic.TryGetValue(type,out toLoadRuntimeAnimatorController);
    //    return toLoadRuntimeAnimatorController;
    //}
    
        //if (!RuntimeAnimatorControllerIdic.ContainsKey(type))
        //{
        //    RuntimeAnimatorController toLoadRuntimeAnimatorController = Resources.Load("Animations/" + type + "/generic_controller", typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;
        //    if (toLoadRuntimeAnimatorController != null)
        //        RuntimeAnimatorControllerIdic.Add(type, toLoadRuntimeAnimatorController);
        //    else
        //        Debug.Log("找不到动画控制器："+"Animations/" + type + "/generic_controller");
        //}else{
        //    if (RuntimeAnimatorControllerIdic[type] == null)
        //    {
        //        RuntimeAnimatorController toLoadRuntimeAnimatorController = Resources.Load("Animations/" + type + "/generic_controller", typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;
        //        if (toLoadRuntimeAnimatorController != null)
        //            RuntimeAnimatorControllerIdic[type] = toLoadRuntimeAnimatorController;
        //        else
        //            Debug.Log("找不到动画控制器："+"Animations/" + type + "/generic_controller");
        //    }
        //}
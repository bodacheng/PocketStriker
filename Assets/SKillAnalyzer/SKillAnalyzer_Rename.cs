using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public partial class SKillAnalyzer
{
    public void ReplaceAnimEventName(string type, string old_name, string new_name)
    {
        if (!(!string.IsNullOrEmpty(old_name)&& new_name != null && new_name != ""))
        {
            return;
        }
        
        var BasicPack = Resources.LoadAll("Animations/" + type + "/" + "BasicPack", typeof(AnimationClip)).ToList();
        var G_Attack_States = Resources.LoadAll("Animations/" + type + "/" + "G_Attack_State", typeof(AnimationClip)).ToList();
        var G_Attack_State_Stays = Resources.LoadAll("Animations/" + type + "/" + "G_Attack_State_Stay", typeof(AnimationClip)).ToList();
        var GMStatess = Resources.LoadAll("Animations/" + type + "/" + "GMStates", typeof(AnimationClip)).ToList();

        var AnimationClips = new List<UnityEngine.Object>();
        foreach (UnityEngine.Object _object in BasicPack)
        {
            AnimationClips.Add(_object as AnimationClip);
        }
        foreach (UnityEngine.Object _object in G_Attack_States)
        {
            AnimationClips.Add(_object as AnimationClip);
        }
        foreach (UnityEngine.Object _object in G_Attack_State_Stays)
        {
            AnimationClips.Add(_object as AnimationClip);
        }
        foreach (UnityEngine.Object _object in GMStatess)
        {
            AnimationClips.Add(_object as AnimationClip);
        }
        foreach (UnityEngine.Object _clip in AnimationClips)
        {
            AnimationClip animationClip = _clip as AnimationClip;
            bool changed = false;
            List<AnimationEvent> evnets = new List<AnimationEvent>();
            foreach (AnimationEvent e in animationClip.events)
            {
                AnimationEvent toSave = e;
                if (e.functionName == old_name)
                {
                    toSave.functionName = new_name;
                    changed = true;
                    Debug.Log("讲动画片段：" + animationClip.name + "的函数" + old_name + "换了新名字" + new_name);
                }
                evnets.Add(toSave);
            }
            if (changed)
            {
                //AnimationClip toSave = Instantiate(animationClip);
                //AnimationUtility.SetAnimationEvents(toSave, evnets.ToArray());
                //AssetDatabase.CreateAsset(toSave, AssetDatabase.GetAssetPath(_clip));
                //AssetDatabase.SaveAssets();
            }
        }
    }
}

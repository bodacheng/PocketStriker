using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class SKillAnalyzer
{
    class EventNameAndAtFrame 
    {
        public string name;
        public float startFrame;
    }
    
    public static readonly List<string> AttackFrameStartMethodNames = new List<string>() {
        "SetRightHandMarkerManager","SetLeftHandMarkerManager",
        "SetRightFootMarkerManager","SetLeftFootMarkerManager",
        "SetRightHandWeaponMarkerManager","SetLeftHandWeaponMarkerManager",
        "SetHeadMarkerManager","SetTailMarkerManager"
    };
    
    static readonly List<string> AttackClearMethodNames = new List<string>() {
        "SetRightHandMarkerManager","SetLeftHandMarkerManager",
        "SetRightFootMarkerManager","SetLeftFootMarkerManager",
        "SetRightHandWeaponMarkerManager","SetLeftHandWeaponMarkerManager",
        "SetHeadMarkerManager","SetTailMarkerManager"
    };
    static readonly List<string> EffectsAttackFrameStartMethodNames = new List<string>()
    {
        "MagicForward","Bullet_shoot_from_body_part","BlastAttack","ReleasePreparedMagic","ReleasePreparedMagicToAir","MagicToEnemy"
    };
    
    public static async UniTask<IDictionary<string, AnimationClip>> AllSkillAnims(string type)
    {
        IDictionary<string, AnimationClip> AnimationClips = new Dictionary<string, AnimationClip>();
        
        var loadPath = Addressables.LoadResourceLocationsAsync("skill_anim");
        await loadPath;
        if (loadPath.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var path in loadPath.Result)
            {
                //Debug.Log(":"+ path);
                Object value = await AddressablesLogic.LoadT<AnimationClip>(path.PrimaryKey);
                if (value != null)
                {
                    var _AnimationClip = (AnimationClip)value;
                    AnimationClips.Add(_AnimationClip.name, _AnimationClip);
                }
            }
        }
        Addressables.Release(loadPath);
        return AnimationClips;
    }
}

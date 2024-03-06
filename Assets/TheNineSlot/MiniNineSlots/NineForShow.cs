using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using dataAccess;
using mainMenu;

public partial class NineForShow : MonoBehaviour
{
    public BOButton A1T, A2T, A3T, B1T, B2T, B3T, C1T, C2T, C3T;
    [SerializeField] Image A1Frame, A2Frame, A3Frame, B1Frame, B2Frame, B3Frame, C1Frame, C2Frame, C3Frame;
    [SerializeField] string abnormalSkillSetEffectKey = "defaultmagic/abnormalSkillSet.prefab";
    [SerializeField] string notQualifiedEffectKey = "defaultmagic/skillSetWarn.prefab";
    [SerializeField] GameObject editSkillIndicator;
    SKStoneItem _a1S, _a2S, _a3S, _b1S, _b2S, _b3S, _c1S, _c2S, _c3S;
    
    public void ClearCurrent()
    {
        if (_a1S != null)
        {
            Destroy(_a1S.gameObject);
            _a1S = null;
        }
        if (_a2S != null)
        {
            Destroy(_a2S.gameObject);
            _a2S = null;
        }
        if (_a3S != null)
        {
            Destroy(_a3S.gameObject);
            _a3S = null;
        }
        if (_b1S != null)
        {
            Destroy(_b1S.gameObject);
            _b1S = null;
        }
        if (_b2S != null)
        {
            Destroy(_b2S.gameObject);
            _b2S = null;
        }
        if (_b3S != null)
        {
            Destroy(_b3S.gameObject);
            _b3S = null;
        }
        if (_c1S != null)
        {
            Destroy(_c1S.gameObject);
            _c1S = null;
        }
        if (_c2S != null)
        {
            Destroy(_c2S.gameObject);
            _c2S = null;
        }
        if (_c3S != null)
        {
            Destroy(_c3S.gameObject);
            _c3S = null;
        }
    }

    void OnDestroy()
    {
        // 当游戏物体被销毁时，取消CancellationTokenSource
        _cancellationTokenSource?.Cancel();
    }

    private CancellationTokenSource _cancellationTokenSource;
    private List<ParticleSystem> effects = new List<ParticleSystem>();

    void DestroyEffects()
    {
        foreach (var effect in effects)
        {
            if (effect != null)
                Destroy(effect.gameObject);
        }
        effects.Clear();
    }
    
    async UniTask SkillSetStateRender(
        Camera fxCamera,
        string a1SkillId, string a2SkillId, string a3SkillId,
        string b1SkillId, string b2SkillId, string b3SkillId,
        string c1SkillId, string c2SkillId, string c3SkillId,
        bool bossMode, bool showEditSkillIndicator)
    {
        var valR = SkillSet.CheckEdit(
            a1SkillId, a2SkillId, a3SkillId,
            b1SkillId, b2SkillId, b3SkillId,
            c1SkillId, c2SkillId, c3SkillId);
        
        if (valR == SkillSet.SkillEditError.UnBalanced || valR == SkillSet.SkillEditError.RepeatedSkill || valR == SkillSet.SkillEditError.NoNormalStart)
        {
            await UniTask.DelayFrame(5);
            await AddEffect(bossMode? abnormalSkillSetEffectKey : notQualifiedEffectKey, fxCamera);
        }
        else
        {
            DestroyEffects();
        }
        // 下面这个环节纯粹是为了队伍编辑画面的技能编辑引导
        if (editSkillIndicator != null)
        {
            editSkillIndicator.SetActive(showEditSkillIndicator &&
                                         (PreScene.target.Focusing != null && PreScene.target.Focusing.id != null) &&
                                         (valR == SkillSet.SkillEditError.UnBalanced 
                                          || valR == SkillSet.SkillEditError.RepeatedSkill 
                                          || valR == SkillSet.SkillEditError.NoNormalStart
                                          || valR == SkillSet.SkillEditError.NotFull));
        }
    }

    async UniTask AddEffect(string address, Camera fxCamera)
    {
        var worldPos = PosCal.GetWorldPos(fxCamera, transform.GetComponent<RectTransform>(), 5f);
        _cancellationTokenSource = new CancellationTokenSource();
        var abnormalSkillSet = await AddressablesLogic.LoadTOnObject<ParticleSystem>(address, gameObject, _cancellationTokenSource);
        if (abnormalSkillSet == null)
        {
            return;
        }
        effects.Add(abnormalSkillSet);
        abnormalSkillSet.gameObject.transform.position = worldPos;
        //abnormalSkillSet.transform.SetParent(transform);
    }

    public async UniTask ShowStones(
        string a1SkillId, string a2SkillId, string a3SkillId,
        string b1SkillId, string b2SkillId, string b3SkillId,
        string c1SkillId, string c2SkillId, string c3SkillId)
    {
        ClearCurrent();
        
        var tasks =  new[]
        {
            Stones.GenerateStoneModel(a1SkillId, false),
            Stones.GenerateStoneModel(a2SkillId, false),
            Stones.GenerateStoneModel(a3SkillId, false),
            Stones.GenerateStoneModel(b1SkillId, false),
            Stones.GenerateStoneModel(b2SkillId, false),
            Stones.GenerateStoneModel(b3SkillId, false),
            Stones.GenerateStoneModel(c1SkillId, false),
            Stones.GenerateStoneModel(c2SkillId, false),
            Stones.GenerateStoneModel(c3SkillId, false)
        };
        
        var results = await UniTask.WhenAll(tasks);
        
        _a1S = results[0];
        _a2S = results[1];
        _a3S = results[2];
        _b1S = results[3];
        _b2S = results[4];
        _b3S = results[5];
        _c1S = results[6];
        _c2S = results[7];
        _c3S = results[8];
        
        Parent();
        
        return;
        
        if (_a1S != null && A1Frame != null)
        {
            A1Frame.color = RefreshFrameColor(_a1S._SkillConfig.SP_LEVEL);
        }
        if (_a2S != null && A2Frame != null)
        {
            A2Frame.color = RefreshFrameColor(_a2S._SkillConfig.SP_LEVEL);
        }
        if (_a3S != null && A3Frame != null)
        {
            A3Frame.color = RefreshFrameColor(_a3S._SkillConfig.SP_LEVEL);
        }
        if (_b1S != null && B1Frame != null)
        {
            B1Frame.color = RefreshFrameColor(_b1S._SkillConfig.SP_LEVEL);
        }
        if (_b2S != null && B2Frame != null)
        {
            B2Frame.color = RefreshFrameColor(_b2S._SkillConfig.SP_LEVEL);
        }
        if (_b3S != null && B3Frame != null)
        {
            B3Frame.color = RefreshFrameColor(_b3S._SkillConfig.SP_LEVEL);
        }
        if (_c1S != null && C1Frame != null)
        {
            C1Frame.color = RefreshFrameColor(_c1S._SkillConfig.SP_LEVEL);
        }
        if (_c2S != null && C2Frame != null)
        {
            C2Frame.color = RefreshFrameColor(_c2S._SkillConfig.SP_LEVEL);
        }
        if (_c3S != null && C3Frame != null)
        {
            C3Frame.color = RefreshFrameColor(_c3S._SkillConfig.SP_LEVEL);
        }
    }
    
    Color RefreshFrameColor(int spLevel)
    {
        switch(spLevel)
        {
            case 1:
                return Color.green;
            case 2:
                return Color.yellow;
            case 3:
                return Color.red;
            default:
                return Color.white;
        }
    }
    
    void Parent()
    {
        void SS(SKStoneItem SK, Button BT)
        {
            if (SK == null || BT == null)
                return;
            SK.transform.SetParent(BT.transform);
            SK.transform.localPosition = Vector3.zero;
            SK.transform.localScale = Vector3.one;
            
            var targetRect = SK.GetComponent<RectTransform>();
            targetRect.anchorMin = new Vector2(0, 0);
            targetRect.anchorMax = new Vector2(1, 1);
            targetRect.offsetMin = new Vector2(0, 0);
            targetRect.offsetMax = new Vector2(0, 0);
            //SK.GetComponent<RectTransform>().rect.Set(0, 0, slotRT.rect.width,slotRT.rect.height);
            SK.gameObject.SetActive(true);
        }

        SS(_a1S, A1T);
        SS(_a2S, A2T);
        SS(_a3S, A3T);
        SS(_b1S, B1T);
        SS(_b2S, B2T);
        SS(_b3S, B3T);
        SS(_c1S, C1T);
        SS(_c2S, C2T);
        SS(_c3S, C3T);
    }
}

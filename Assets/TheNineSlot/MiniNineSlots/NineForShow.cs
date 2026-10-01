using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using dataAccess;
using mainMenu;

public partial class NineForShow : MonoBehaviour
{
#if UNITY_EDITOR
    // Per-view fault injection for Editor regression; omitted from player builds.
    System.Func<string, Cysharp.Threading.Tasks.UniTask<SKStoneItem>> iconLoaderForValidation;
#endif
    Cysharp.Threading.Tasks.UniTask<SKStoneItem> LoadIcon(string id)
    {
#if UNITY_EDITOR
        if (iconLoaderForValidation != null) return iconLoaderForValidation(id);
#endif
        return Stones.GenerateStoneModel(id, false);
    }

    int stoneRequestVersion;

    public BOButton A1T, A2T, A3T, B1T, B2T, B3T, C1T, C2T, C3T;
    [SerializeField] Image A1Frame, A2Frame, A3Frame, B1Frame, B2Frame, B3Frame, C1Frame, C2Frame, C3Frame;
    [SerializeField] string abnormalSkillSetEffectKey = "defaultmagic/abnormalSkillSet.prefab";
    [SerializeField] string notQualifiedEffectKey = "defaultmagic/skillSetWarn.prefab";
    [SerializeField] GameObject editSkillIndicator;
    SKStoneItem _a1S, _a2S, _a3S, _b1S, _b2S, _b3S, _c1S, _c2S, _c3S;

    // Presentation is opt-in for preparation pages. Address the authored frames
    // explicitly so already loaded stone artwork and status effects stay intact.
    public void StylePreparationSlots(Color color)
    {
        foreach (var frame in new[] { A1Frame, A2Frame, A3Frame, B1Frame, B2Frame, B3Frame, C1Frame, C2Frame, C3Frame })
            StylePreparationFrame(frame, color);
        foreach (var button in new[] { A1T, A2T, A3T, B1T, B2T, B3T, C1T, C2T, C3T })
            if (button != null) StylePreparationFrame(button.GetComponent<Image>(), color);
    }

    static void StylePreparationFrame(Image frame, Color color)
    {
        if (frame == null) return;
        color.a = Mathf.Min(frame.color.a, color.a);
        frame.color = color;
        foreach (var shadow in frame.GetComponents<Shadow>()) shadow.enabled = false;
    }
    
    public void ClearCurrent()
    {
        // Clearing also invalidates loads that have not returned yet.
        stoneRequestVersion++;
        if (_a1S != null)
        {
            DiscardIcon(_a1S);
            _a1S = null;
        }
        if (_a2S != null)
        {
            DiscardIcon(_a2S);
            _a2S = null;
        }
        if (_a3S != null)
        {
            DiscardIcon(_a3S);
            _a3S = null;
        }
        if (_b1S != null)
        {
            DiscardIcon(_b1S);
            _b1S = null;
        }
        if (_b2S != null)
        {
            DiscardIcon(_b2S);
            _b2S = null;
        }
        if (_b3S != null)
        {
            DiscardIcon(_b3S);
            _b3S = null;
        }
        if (_c1S != null)
        {
            DiscardIcon(_c1S);
            _c1S = null;
        }
        if (_c2S != null)
        {
            DiscardIcon(_c2S);
            _c2S = null;
        }
        if (_c3S != null)
        {
            DiscardIcon(_c3S);
            _c3S = null;
        }
    }

    void OnDestroy()
    {
        stoneRequestVersion++;
        // 当游戏物体被销毁时，取消CancellationTokenSource
        _cancellationTokenSource?.Cancel();
    }

    private CancellationTokenSource _cancellationTokenSource;
    
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
            if (nineSlotEffect != null)
                Destroy(nineSlotEffect.gameObject);
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

    private ParticleSystem nineSlotEffect;
    async UniTask AddEffect(string address, Camera fxCamera)
    {
        if (nineSlotEffect != null && nineSlotEffect.gameObject.activeSelf)
            return;
        var worldPos = PosCal.GetWorldPos(fxCamera, transform.GetComponent<RectTransform>(), 5f);
        _cancellationTokenSource = new CancellationTokenSource();
        nineSlotEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>(address, gameObject, _cancellationTokenSource);
        if (nineSlotEffect == null)
        {
            return;
        }
        nineSlotEffect.gameObject.transform.position = worldPos;
        //abnormalSkillSet.transform.SetParent(transform);
    }

    public async UniTask ShowStones(
        string a1SkillId, string a2SkillId, string a3SkillId,
        string b1SkillId, string b2SkillId, string b3SkillId,
        string c1SkillId, string c2SkillId, string c3SkillId)
    {
        ClearCurrent();

        int version = stoneRequestVersion;
        var pending = new SKStoneItem[9];
        bool abandoned = false;

        // WhenAll can fail before the other loads finish. Each load retains
        // ownership until accepted, including results arriving after a failure.
        async UniTask LoadSlot(int index, string id)
        {
            var item = await LoadIcon(id);
            if (abandoned || this == null || version != stoneRequestVersion)
                DiscardIcon(item);
            else
                pending[index] = item;
        }

        try
        {
            await UniTask.WhenAll(
                LoadSlot(0, a1SkillId), LoadSlot(1, a2SkillId), LoadSlot(2, a3SkillId),
                LoadSlot(3, b1SkillId), LoadSlot(4, b2SkillId), LoadSlot(5, b3SkillId),
                LoadSlot(6, c1SkillId), LoadSlot(7, c2SkillId), LoadSlot(8, c3SkillId));
        }
        catch
        {
            abandoned = true;
            foreach (var item in pending) DiscardIcon(item);
            throw;
        }
        if (this == null || version != stoneRequestVersion)
        {
            foreach (var item in pending) DiscardIcon(item);
            return;
        }

        _a1S = pending[0]; _a2S = pending[1]; _a3S = pending[2];
        _b1S = pending[3]; _b2S = pending[4]; _b3S = pending[5];
        _c1S = pending[6]; _c2S = pending[7]; _c3S = pending[8];
        Parent();
    }

    static void DiscardIcon(SKStoneItem item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }

    void Parent()
    {
        void SS(SKStoneItem SK, Button BT)
        {
            if (SK == null) return;
            if (BT == null)
            {
                DiscardIcon(SK);
                return;
            }
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

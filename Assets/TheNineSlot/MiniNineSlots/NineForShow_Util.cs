using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

// 抽卡技能石细节显示
public partial class NineForShow : MonoBehaviour
{
    private int _clickedSlot = 1;
    public int ClickedSlot => _clickedSlot;
    
    readonly IDictionary<int, ParticleSystem> _slotEffects = new Dictionary<int, ParticleSystem>();

    public List<BOButton> AllButton()
    {
        return new List<BOButton>()
        {
            A1T, A2T, A3T,
            B1T, B2T, B3T,
            C1T, C2T, C3T
        };
    }
    
    List<SKStoneItem> AllStones()
    {
        return new List<SKStoneItem>()
        {
            A1T.transform.GetComponentInChildren<SKStoneItem>(), 
            A2T.transform.GetComponentInChildren<SKStoneItem>(),
            A3T.transform.GetComponentInChildren<SKStoneItem>(),
            B1T.transform.GetComponentInChildren<SKStoneItem>(),
            B2T.transform.GetComponentInChildren<SKStoneItem>(),
            B3T.transform.GetComponentInChildren<SKStoneItem>(),
            C1T.transform.GetComponentInChildren<SKStoneItem>(),
            C2T.transform.GetComponentInChildren<SKStoneItem>(),
            C3T.transform.GetComponentInChildren<SKStoneItem>()
        };
    }
    
    public static async UniTask RefreshSlotEffects(int slotNum, int eX, Vector3 pos, Transform releaseTarget, 
        IDictionary<int, ParticleSystem> _slotEffects, float scale = 1, int targetLayer = 0)
    {
        if (_slotEffects.ContainsKey(slotNum) && _slotEffects[slotNum] != null)
        {
            Destroy(_slotEffects[slotNum].gameObject);
        }
        
        string effectName;
        switch (eX)
        {
            case -1:
                return;
            case 1:
                effectName = "SlotEffects/ex1";
                break;
            case 2:
                effectName = "SlotEffects/ex2";
                break;
            case 3:
                effectName = "SlotEffects/ex3";
                break;
            default:
                effectName = "SlotEffects/normal";
                break;
        }
        var slotEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>(effectName, releaseTarget.gameObject);

        var slotT = slotEffect.transform;

        var oldScale = slotT.localScale;
        slotT.localScale = new Vector3(oldScale.x * PosCal.TempRate() * scale, oldScale.y * PosCal.TempRate() * scale, oldScale.z);
        
        //slotEffect.transform.SetParent(parent);
        DicAdd<int, ParticleSystem>.Add(_slotEffects, slotNum, slotEffect);
        slotEffect.gameObject.name = "slotEffect"+ slotNum;
        slotEffect.gameObject.layer = targetLayer;
        slotT.position = pos;
        slotEffect.Play(true);
    }
    
    public async UniTask SkillSetInfoOfUnitOnArcadePage(SkillSet set)
    {
        await UniTask.WhenAll(
            SkillSetStateRender(
                PreScene.target.postProcessCamera,
                set.a1, set.a2, set.a3,
                set.b1, set.b2, set.b3,
                set.c1, set.c2, set.c3, 
                true, true
            ),
            ShowStones(
                set.a1, set.a2, set.a3,
                set.b1, set.b2, set.b3,
                set.c1, set.c2, set.c3
            )
        );
    }
    
    public void AddOnClickToSlots(Action<string> onClickStone)
    {
        A1T.SetListener(() => { AddOnClickToBtn(A1T, onClickStone);});
        A2T.SetListener(() => { AddOnClickToBtn(A2T, onClickStone);});
        A3T.SetListener(() => { AddOnClickToBtn(A3T, onClickStone);});
        
        B1T.SetListener(() => { AddOnClickToBtn(B1T, onClickStone);});
        B2T.SetListener(() => { AddOnClickToBtn(B2T, onClickStone);});
        B3T.SetListener(() => { AddOnClickToBtn(B3T, onClickStone);});
        
        C1T.SetListener(() => { AddOnClickToBtn(C1T, onClickStone);});
        C2T.SetListener(() => { AddOnClickToBtn(C2T, onClickStone);});
        C3T.SetListener(() => { AddOnClickToBtn(C3T, onClickStone);});
    }
    
    void AddOnClickToBtn(Button targetButton, Action<string> onClickStone)
    {
        var item = targetButton.transform.GetComponentInChildren<SKStoneItem>();
        if (item != null)
        {
            onClickStone(item._SkillConfig.RECORD_ID);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="camera"></param>
    /// <param name="scale">
    /// 技能编辑画面的slot特效和格子尺寸的适配关系，前提无非是他们的prefab尺寸正好就是适配起来了，这个是前提，
    /// 然后技能进化画面里面的那个格子在Canvas上的长度是技能编辑画面中的两倍，把2给撑上正好尺寸也适配了
    /// </param>
    public async UniTask RefreshEffects(Camera camera, float scale)
    {
        await UniTask.DelayFrame(1);// wait for the UI Layer to be stable.Otherwise pos caculation will be wrong at the start
        var allStones = AllStones();
        var tasks = new List<UniTask>();
        for (var index = 0; index < allStones.Count; index++)
        {
            var item = allStones[index];
            if (item != null)
            {
                BOButton parentBtn = item.GetComponentInParent<BOButton>();
                var worldPos = PosCal.GetWorldPos(camera,
                    parentBtn.transform.GetComponent<RectTransform>(), 5f);
                var task = RefreshSlotEffects(
                    index + 1,
                    item != null ? item._SkillConfig.SP_LEVEL : -1,
                    worldPos,
                    parentBtn.transform,
                    _slotEffects,
                    scale,
                    5
                );
                tasks.Add(task);
            }
        }
        await UniTask.WhenAll(tasks);
    }
}

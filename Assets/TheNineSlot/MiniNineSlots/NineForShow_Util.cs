using System;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

// 抽卡技能石细节显示
public partial class NineForShow : MonoBehaviour
{
    public async UniTask SkillSetInfoOfUnitOnArcadePage(SkillSet set)
    {
        await UniTask.WhenAll(
            SkillSetStateRender(
                PreScene.target.postProcessCamera,
                set.a1, set.a2, set.a3,
                set.b1, set.b2, set.b3,
                set.c1, set.c2, set.c3, 
                true, false
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
        A1T.onClick.RemoveAllListeners();
        A1T.onClick.AddListener(() => { AddOnClickToBtn(A1T, onClickStone);});
        A2T.onClick.RemoveAllListeners();
        A2T.onClick.AddListener(() => { AddOnClickToBtn(A2T, onClickStone);});
        A3T.onClick.RemoveAllListeners();
        A3T.onClick.AddListener(() => { AddOnClickToBtn(A3T, onClickStone);});
        
        B1T.onClick.RemoveAllListeners();
        B1T.onClick.AddListener(() => { AddOnClickToBtn(B1T, onClickStone);});
        B2T.onClick.RemoveAllListeners();
        B2T.onClick.AddListener(() => { AddOnClickToBtn(B2T, onClickStone);});
        B3T.onClick.RemoveAllListeners();
        B3T.onClick.AddListener(() => { AddOnClickToBtn(B3T, onClickStone);});
        
        C1T.onClick.RemoveAllListeners();
        C1T.onClick.AddListener(() => { AddOnClickToBtn(C1T, onClickStone);});
        C2T.onClick.RemoveAllListeners();
        C2T.onClick.AddListener(() => { AddOnClickToBtn(C2T, onClickStone);});
        C3T.onClick.RemoveAllListeners();
        C3T.onClick.AddListener(() => { AddOnClickToBtn(C3T, onClickStone);});
    }
    
    void AddOnClickToBtn(Button targetButton, Action<string> onClickStone)
    {
        var item = targetButton.transform.GetComponentInChildren<SKStoneItem>();
        if (item != null)
        {
            onClickStone(item._SkillConfig.RECORD_ID);
        }
    }
}

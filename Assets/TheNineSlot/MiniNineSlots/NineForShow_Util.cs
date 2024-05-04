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
}

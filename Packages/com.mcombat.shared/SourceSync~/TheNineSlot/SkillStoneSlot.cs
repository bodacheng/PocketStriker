using UnityEngine;
using dataAccess;

// 编辑技能的两种模式，归根结底是9宫格自身的两种模式，即SkillStoneSlot的两种模式。
// 从我们使用那个插件开发这个环节至今，格子bug的根源其实是开始我们没有发现GetItem函数的正确发挥作用依赖于在那之前先运行updateMyItem函数，导致GetItem结果不正确。
// 造成了本来运行顺序就不怎么清晰一插件看起来更乱。
// 然而现在，仍然有一个潜在问题存在，那就是在某一个九宫格的cell下可能出现两个石头。
// 这个是把两个新石头拖入九宫后不停对两者进行位置移动所造成的。一旦这个现象出现就可能产生随之而来的一系列bug。
// 但这个bug我们是以showOrigin()函数内强制清空所有石头的方法解决的。
// 如果showOrigin()没给解决这个事情那那个bug还是会出现，说明这个环节某个部分还是存在些逻辑问题。

public class SkillStoneSlot
{
    public readonly StoneCell _cell;
    public readonly int num;
    public SkillStoneSlot(int num, StoneCell cell)
    {
        this.num = num;
        this._cell = cell;
    }

    public void TakeASkillStoneFromBoxToSlot(string instanceID)
    {
        var stoneModel = Stones.GetRenderModel(instanceID);
        if (stoneModel == null)
        {
            Debug.Log("Cant find stone model. InstanceID:"+ instanceID);
            return;
        }
        _cell.AddItem(stoneModel);
    }
}
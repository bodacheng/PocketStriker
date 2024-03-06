using System.Collections.Generic;

namespace dataAccess
{
    public static partial class Stones
    {
        // 这个游戏玩家拥有的最大角色数是多少？我不妨就把拥有角色数量最为个参数去用？
        // 以下函数创造的目的是用以评估玩家盈余的技能石数量是不是太多，key是skillid，value是玩家拥有的卡牌instance列表
        public static Dictionary<string, List<string>> CheckForUpdates()
        {
            var unitsCount = Units.Dic.Count;
            var exceededStones = new Dictionary<string, List<string>>();
            foreach (var skillId in SkillConfigTable.SkillConfigRefDic.Keys)
            {
                var instanceIds = GetMyStonesBySkillID(skillId);
                if (instanceIds.Count > unitsCount * 5) // 5个技能石头可合并成一个技能石，这个条件代表这种技能石拥有过多
                {
                    exceededStones.Add(skillId, instanceIds);
                }
            }
            return exceededStones;
        }
    }
}
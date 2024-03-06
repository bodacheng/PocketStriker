using System;
using Skill;

[Serializable]
public class UnitConfig
{
    public string RECORD_ID;
    public string TYPE;
    public string REAL_NAME;
    public string showNameEN;
    public string showNameCN;
    public string showNameJP;
    public Element element = Element.lightMagic;
    public string BASIC_MOVEMENT_PACK = "basic_anim";//monsterTable BasicMoveSet
    public MoveType MoveType = MoveType.Move_normal;//monsterTable moveType
    public RushType RushType = RushType.RushBack;//monsterTable accSKill
    public bool DEFENDABLE_FLAG = true;
    public string InstructionEN;
    public string InstructionCH;
    public string InstructionJP;
    public int RARITY_LEVEL = 3;

    public UnitInfo GetTestCharConfig(string localID)
    {
        UnitInfo characterDataInfo = new UnitInfo
        {
            id = localID,
            r_id = RECORD_ID, // 确切的说这个也就是角色的pretab编号，最后也就是数据库里master table的主key。
            set = null
        };
        return characterDataInfo;
    }
}
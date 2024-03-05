using Skill;
using System;

[Serializable]
public partial class SkillSet {

    public string a1, a2, a3;
    public string b1, b2, b3;
    public string c1, c2, c3;
    
    private bool _def;
    private MoveType _moveType;
    private RushType _rushType;
    
    public bool GetD()
    {
        return _def;
    }

    public MoveType GetM()
    {
        return _moveType;
    }

    public RushType GetR()
    {
        return _rushType;
    }
    
    public SkillSet()
    {
        a1 = null; a2 = null; a3 = null;
        b1 = null; b2 = null; b3 = null;
        c1 = null; c2 = null; c3 = null;
        
        _moveType = MoveType.Move_normal;
        _def = false;
        _rushType = RushType.Rush;
    }

    public SkillSet(MoveType moveType, bool canDefend, RushType rushType)
    {
        a1 = null; a2 = null; a3 = null;
        b1 = null; b2 = null; b3 = null;
        c1 = null; c2 = null; c3 = null;

        this._moveType = moveType;
        this._def = canDefend;
        this._rushType = rushType;
    }

    public SkillSet DeepCopy()
    {
        return (SkillSet)MemberwiseClone();
    }

    public void SetPassive(bool _Def, MoveType _MoveType, RushType _RushType)
    {
        _def = _Def;
        _moveType = _MoveType;
        _rushType = _RushType;
    }

    public SkillEditError CheckEdit()
    {
        return CheckEdit(
            a1, a2, a3,
            b1, b2, b3,
            c1, c2, c3
        );
    }
}

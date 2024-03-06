#if UNITY_EDITOR

public partial class StageEditor
{
    int _targetSlot = 0;
    void SetSkillId(string skillID)
    {
        switch(_targetSlot)
        {
            case 1:
            _focusingUnitInfo.set.a1 = skillID;
            break;
            case 2:
            _focusingUnitInfo.set.a2 = skillID;
            break;
            case 3:
            _focusingUnitInfo.set.a3 = skillID;
            break;
            case 4:
            _focusingUnitInfo.set.b1 = skillID;
            break;
            case 5:
            _focusingUnitInfo.set.b2 = skillID;
            break;
            case 6:
            _focusingUnitInfo.set.b3 = skillID;
            break;
            case 7:
            _focusingUnitInfo.set.c1 = skillID;
            break;
            case 8:
            _focusingUnitInfo.set.c2 = skillID;
            break;
            case 9:
            _focusingUnitInfo.set.c3 = skillID;
            break;
        }
        _focusingUnitInfo.set.SortNineAndTwo();
    }
    
    string GetFocusSkillId()
    {
        switch(_targetSlot)
        {
            case 1:
            return _focusingUnitInfo.set.a1;
            case 2:
            return _focusingUnitInfo.set.a2;
            case 3:
            return _focusingUnitInfo.set.a3;
            case 4:
            return _focusingUnitInfo.set.b1;
            case 5:
            return _focusingUnitInfo.set.b2;
            case 6:
            return _focusingUnitInfo.set.b3;
            case 7:
            return _focusingUnitInfo.set.c1;
            case 8:
            return _focusingUnitInfo.set.c2;
            case 9:
            return _focusingUnitInfo.set.c3;
            default:
            return null;
        }
    }
}
#endif
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public class EvolutionManager
{
    private int _costLimit;
    public int EvolutionCount { get; set; }
    
    public string[] RandomSkillList(string type, SkillSet skillSet)
    {
        int[] exType;

        switch (EvolutionCount)
        {
            case 1:
                exType = new[] {1};
                break;
            case 2:
                exType = new[] {2};
                break;
            case 3:
                exType = new[] {2, 3};
                break;
            default:
                exType = new[] {skillSet.GetLowestSpLevel()+1}; // temp logic
                break;
        }
        
        var filterForm = new SkillStonesBox.StoneFilterForm
        {
            Type = type,
            ExType = exType,
            Close = false,
            Near = false,
            Far = false
        };
        var skill1 =  SkillSet.RandomSkillIDOfStone(filterForm);
        var skill2 = SkillSet.RandomSkillIDOfStone(filterForm, new List<string>(){ skill1 });
        var skill3 = SkillSet.RandomSkillIDOfStone(filterForm, new List<string>(){ skill1, skill2 });
        return new[] { skill1, skill2, skill3 };
    }
    
    public async UniTask ChangeSkill(Data_Center focusUnit, int targetSlotIndex, string skillId)
    {
        switch (targetSlotIndex)
        {
            case 1:
                focusUnit.UnitInfo.set.a1 = skillId;
                break;
            case 2:
                focusUnit.UnitInfo.set.a2 = skillId;
                break;
            case 3:
                focusUnit.UnitInfo.set.a3 = skillId;
                break;
            case 4:
                focusUnit.UnitInfo.set.b1 = skillId;
                break;
            case 5:
                focusUnit.UnitInfo.set.b2 = skillId;
                break;
            case 6:
                focusUnit.UnitInfo.set.b3 = skillId;
                break;
            case 7:
                focusUnit.UnitInfo.set.c1 = skillId;
                break;
            case 8:
                focusUnit.UnitInfo.set.c2 = skillId;
                break;
            case 9:
                focusUnit.UnitInfo.set.c3 = skillId;
                break;
        }
        var unitConfig = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(focusUnit.UnitInfo.r_id));
        var _layer = UILayerLoader.Get<FightingStepLayer>();
        await UniTask.WhenAll(
            focusUnit.Step2Initialize(unitConfig.TYPE, unitConfig.element, focusUnit.UnitInfo.set, 1),
            _layer.InputsManager.ElementRegister(unitConfig.element, focusUnit.UnitInfo)
        );
        
        focusUnit.SetAT();
        focusUnit._MyBehaviorRunner.ChangeToWaitingState();

        if (EvolutionCount == 2)
        {
            focusUnit.FightDataRef.CriticalGaugeMode = CriticalGaugeMode.DoubleGain;
        }
        if (EvolutionCount >= 3)
        {
            focusUnit.FightDataRef.CriticalGaugeMode = CriticalGaugeMode.Unlimited;
        }
    }
}

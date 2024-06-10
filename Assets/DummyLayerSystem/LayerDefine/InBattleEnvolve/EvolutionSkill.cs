using mainMenu;
using UnityEngine;

public class EvolutionSkill : MonoBehaviour
{
    [SerializeField] private SkillStoneDetail skillStoneDetail;
    [SerializeField] private BOButton btn;
    public BOButton Btn => btn;
    
    public void ShowIcon(string skillID)
    {
        var config = SkillConfigTable.GetSkillConfigByRecordId(skillID);
        skillStoneDetail.RefreshInfo(config);
        skillStoneDetail.IconForShow(skillID);
    }
}

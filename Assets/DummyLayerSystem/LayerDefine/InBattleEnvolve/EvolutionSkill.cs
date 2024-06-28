using mainMenu;
using UnityEngine;

public class EvolutionSkill : MonoBehaviour
{
    [SerializeField] private SkillStoneDetail skillStoneDetail;
    [SerializeField] private BOButton btn;
    [SerializeField] private Animator animator;
    public BOButton Btn => btn;
    public Animator Animator => animator;
    
    public void ShowIcon(string skillID, float size)
    {
        var config = SkillConfigTable.GetSkillConfigByRecordId(skillID);
        skillStoneDetail.RefreshInfo(config);
        skillStoneDetail.IconForShow(skillID, size);
    }
}

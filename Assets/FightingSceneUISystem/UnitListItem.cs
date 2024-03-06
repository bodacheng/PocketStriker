using UnityEngine;

public class UnitListItem : MonoBehaviour
{
    [SerializeField] private GameObject perfectSetFlg;
    [SerializeField] private GameObject hasEquipFlg;

    public void DecideSkillEquipFlg(SkillSet.SkillEditError skillEditError)
    {
        perfectSetFlg.SetActive(skillEditError == SkillSet.SkillEditError.Perfect);
        hasEquipFlg.SetActive(skillEditError != SkillSet.SkillEditError.Empty &&
                              skillEditError != SkillSet.SkillEditError.Perfect);
    }
}

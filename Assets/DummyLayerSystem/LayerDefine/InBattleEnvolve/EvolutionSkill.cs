using dataAccess;
using UnityEngine;
using UnityEngine.UI;

public class EvolutionSkill : MonoBehaviour
{
    [SerializeField] private BOButton btn;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private Text showName;
    [SerializeField] private Text intro;
    public BOButton Btn => btn;
    
    public async void ShowIcon(string skillID)
    {
        var item = await Stones.GenerateStoneModel(skillID, false);
        item.transform.SetParent(iconParent);
        item.gameObject.SetActive(true);
        item.transform.localPosition = Vector3.zero;
        item.transform.localScale = Vector3.one;
        item.transform.GetComponent<RectTransform>().sizeDelta = iconParent.transform.GetComponent<RectTransform>().sizeDelta;
        
        var config = SkillConfigTable.GetSkillConfigByRecordId(skillID);
        
        if (PlayerAccountInfo.Me.TitleDisplayName != null && PlayerAccountInfo.Me.TitleDisplayName.Contains("IconDev"))
        {
            showName.text = config.RECORD_ID +"."+ SkillNameTable.GetSkillName(config.RECORD_ID);
        }
        else
        {
            showName.text = SkillNameTable.GetSkillName(config.RECORD_ID);    
        }
        
        intro.text = SkillNameTable.GetSkillIntro(config.RECORD_ID);
    }
}

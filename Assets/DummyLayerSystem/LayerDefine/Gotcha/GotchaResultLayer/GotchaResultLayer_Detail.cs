using mainMenu;
using UnityEngine;

public partial class GotchaResultLayer : UILayer
{
    [SerializeField] RectTransform stoneDetailT;
    [SerializeField] SkillStoneDetail _stoneDetail;
    [SerializeField] GotchaBtn GDGotchaBtn;
    [SerializeField] GotchaBtn DMGotchaBtn;
    
    public void ShowDetail(string skillId)
    {
        stoneDetailT.gameObject.SetActive(true);
        var sc = SkillConfigTable.GetSkillConfigByRecordId(skillId);
        _stoneDetail.RefreshInfo(sc);
    }

    void ClearDetail()
    {
        stoneDetailT.gameObject.SetActive(false);
        _stoneDetail.Clear();
    }
}

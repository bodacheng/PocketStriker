using UnityEngine;
using UnityEngine.UI;

public class RewardUI : MonoBehaviour
{
    [SerializeField] Text rewardDM;
    [SerializeField] Text rewardGD;
    [SerializeField] Image rewardDMIcon;
    [SerializeField] Image rewardGDIcon;
    [SerializeField] GameObject dmGotMark;
    [SerializeField] GameObject gdGotMark;
    
    public void AwardRender(bool got)
    {
        rewardDM.color = new Color(rewardDM.color.r, rewardDM.color.g, rewardDM.color.b, got ? 0.3f : 1f);
        rewardGD.color = new Color(rewardGD.color.r, rewardGD.color.g, rewardGD.color.b, got ? 0.3f : 1f);
        rewardDMIcon.color = new Color(rewardDMIcon.color.r, rewardDMIcon.color.g, rewardDMIcon.color.b, got ? 0.3f : 1f);
        rewardGDIcon.color = new Color(rewardGDIcon.color.r, rewardGDIcon.color.g, rewardGDIcon.color.b, got ? 0.3f : 1f);
        
        dmGotMark.SetActive(got);
        gdGotMark.SetActive(got);
    }
    
    public void ShowRewards(int awardDM, int awardGD)
    {
        rewardDM.text = awardDM.ToString();
        rewardGD.text = awardGD.ToString();
    }
}

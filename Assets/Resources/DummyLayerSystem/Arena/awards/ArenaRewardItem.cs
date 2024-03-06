using UnityEngine;
using UnityEngine.UI;

public class ArenaRewardItem : MonoBehaviour
{
    [SerializeField] private ArenaRankIcon rankIcon;
    [SerializeField] private Text arenaPoint;
    [SerializeField] private Text dmAmount;
    
    public void Set(int pointStage, Award award)
    {
        rankIcon.Set(pointStage);
        arenaPoint.text = pointStage.ToString();
        dmAmount.text = award.d.ToString();
    }
}

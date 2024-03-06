using UnityEngine;
using UnityEngine.UI;

public class ArenaRankIcon : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite[] rankIcons; // 暂定13个等级 
    [SerializeField] private Animator _animator;
    
    public void Set(int point)
    {
        var rank = PlayFabSetting.ArenaPointToRank(point);
        _image.sprite = rankIcons[rank];
    }
    
    public void RankUpAnim()
    {
        _animator.SetTrigger("rankChange");
    }
}
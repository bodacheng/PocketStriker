using System;
using UnityEngine;
using UnityEngine.UI;

public class ArenaNewSeason : UILayer
{
    [SerializeField] Text lastSeasonPoint;
    [SerializeField] Text newSeasonStartPoint;
    [SerializeField] ArenaRankIcon arenaRankIconLastSeason;
    [SerializeField] ArenaRankIcon arenaRankIconNewSeason;
    [SerializeField] Button closeBtn;

    public void Setup(int lastSeasonPoint, int newSeasonStartPoint, Action close)
    {
        this.lastSeasonPoint.text = lastSeasonPoint.ToString();
        this.newSeasonStartPoint.text = newSeasonStartPoint.ToString();
        
        arenaRankIconLastSeason.Set(lastSeasonPoint);
        arenaRankIconNewSeason.Set(newSeasonStartPoint);
        
        closeBtn.onClick.AddListener(() =>
        {
            close();
        });
    }
}

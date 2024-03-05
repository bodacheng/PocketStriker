using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using ModelView;
using UnityEngine;
using UnityEngine.UI;

public class RankingLayer : UILayer
{
    [SerializeField] VerticalLayoutGroup enemiesT;
    [SerializeField] ArenaFightTeamDisplay myArenaFightTeamDisplay;
    [SerializeField] ArenaFightTeamDisplay arenaFightTeamDisplayPrefab;
    [SerializeField] DedicatedCameraConnector connector;
    [SerializeField] float cameraConnectorBottomSpace = 1420;
    [SerializeField] NineForShow miniNineForShow;
    
    void OnClickUnitIcon(UnitInfo unitInfo)
    {
        ResizeCameraConnectorRefTopAndSideWidth(connector.GetComponent<RectTransform>(), (Screen.height - Screen.safeArea.size.y), cameraConnectorBottomSpace);
        
        var set = unitInfo.set;
        miniNineForShow.ShowStones(
            set.a1, set.a2, set.a3,
            set.b1, set.b2, set.b3,
            set.c1, set.c2, set.c3
        ).Forget();
        
        miniNineForShow.AddOnClickToSlots((RECORD_ID) =>
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
            connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
        });
        var unitConfig = Units.GetUnitConfig(unitInfo.r_id);
        BackGroundPS.target.ChangeBGByElement(unitConfig.element);
        connector.ShowModel(unitInfo.r_id).Forget();
    }

    public void SetMyLeaderboardInfo(LeaderboardInfo myTeamInfo)
    {
        myArenaFightTeamDisplay.ArenaRankingShow(myTeamInfo, OnClickUnitIcon);
    }
    
    public void DisplayOpponents(List<LeaderboardInfo> leaderboards)
    {
        float rectHeight = 0;
        UnitInfo firstShowUnitInfo = null;
        foreach (var t in leaderboards)
        {
            var o = Instantiate(arenaFightTeamDisplayPrefab, enemiesT.transform, true);
            o.ArenaRankingShow(t, OnClickUnitIcon);
            var transform1 = o.transform;
            transform1.localPosition = Vector3.zero;
            transform1.localScale = Vector3.one;
            o.gameObject.SetActive(true);
            rectHeight += o.GetComponent<RectTransform>().rect.height + enemiesT.spacing;

            if (firstShowUnitInfo == null)
                firstShowUnitInfo = t.Team.FirstOrDefault()?.value;
        }
        if (firstShowUnitInfo != null)
            OnClickUnitIcon(firstShowUnitInfo);
        
        enemiesT.GetComponent<RectTransform>().sizeDelta = 
            new Vector2(enemiesT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);
    }
}

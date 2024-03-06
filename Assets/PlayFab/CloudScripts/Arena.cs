using UnityEngine;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;
using dataAccess;

public partial class CloudScript
{
    public static void ArenaDefendTeamSave(MultiDic<int, int, UnitInfo> info, Action<bool> finished)
    {
        if (info.GetValues().Count != 3)
        {
            Debug.Log("No enough member.");
        }
        
        foreach (var kv in info.GetValues())
        {
            var skillList = Stones.GetEquippingStones(kv.id);
            if (skillList.Count != 9)
            {
                Debug.Log("No enough skill.");
            }
        }
        
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = "ArenaDefendTeamSave",
                FunctionParameter = new
                {
                    Team = info._SerializableSets,
                    resetArenaPoint = PlayerAccountInfo.Me.arenaPoint == -1
                },
                GeneratePlayStreamEvent = true
            },
            (ExecuteCloudScriptResult result) =>
            {
                var jsonResult = (PlayFab.Json.JsonObject) result.FunctionResult;
                jsonResult.TryGetValue("success", out var succeed);
                jsonResult.TryGetValue("arenapoint", out var arenaPointObject);
                if (arenaPointObject != null)
                {
                    Int32.TryParse(arenaPointObject.ToString(), out var arenaPoint);
                    PlayerAccountInfo.Me.arenaPoint = arenaPoint;
                }
                
                if ((bool)succeed)
                {
                    finished.Invoke(true);
                }
                else
                {
                    finished.Invoke(false);
                }
            },
            error =>
            {
                finished.Invoke(false);
            }
        );
    }
    
    public static void GetLeaderboardAroundUser(Action<List<LeaderboardInfo>> success, Action fail)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "GetLeaderboardAroundUser",
                GeneratePlayStreamEvent = false
            },
            (result) => {
                try
                {
                    var jsonResult = (PlayFab.Json.JsonObject) result.FunctionResult;
                    if (jsonResult == null)
                    {
                        Debug.Log(" 远程结果为空 ");
                        return;
                    }
                    
                    jsonResult.TryGetValue("teamInfos", out var teamInfos);
                    var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(teamInfos);
                    var opponents = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<List<LeaderboardInfo>>(json);
                    success.Invoke(opponents);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    fail.Invoke();
                }
            }
        );
    }
    
    public static void GetLeaderboard(Action<List<LeaderboardInfo>> success)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "GetLeaderboard",
                GeneratePlayStreamEvent = true
            },
            (result) => {
                try
                {
                    var jsonResult = (PlayFab.Json.JsonObject) result.FunctionResult;
                    jsonResult.TryGetValue("teamInfos", out var teamInfos);
                    var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(teamInfos);
                    var opponents = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<List<LeaderboardInfo>>(json);
                    success.Invoke(opponents);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        );
    }
    
    /// <summary>
    /// success : old point, current point, award currency
    /// </summary>
    /// <param name="mePoint"></param>
    /// <param name="opponentPoint"></param>
    /// <param name="success"></param>
    public static void ArenaPointUp(PlayerLeaderboardEntry meLeaderboardEntry, PlayerLeaderboardEntry opponentLeaderboardEntry, Action<int, int, int> success)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "ArenaPointUp",
                FunctionParameter = 
                    new
                    {
                        mePoint = meLeaderboardEntry.StatValue,
                        opponentPoint = opponentLeaderboardEntry.StatValue,
                        mePosition = meLeaderboardEntry.Position,
                        opponentPosition = opponentLeaderboardEntry.Position
                    },
                GeneratePlayStreamEvent = true
            },
            (result) => {
                var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
                var currentPoint = (jsonResult.ContainsKey("currentPoint") ? jsonResult["currentPoint"] : 0).ToString();
                int.TryParse(currentPoint, out var currentPointInt);

                var awardDM = 0;
                var oldRank = PlayFabSetting.ArenaPointToRank(PlayerAccountInfo.Me.arenaPoint);
                var newRank =  PlayFabSetting.ArenaPointToRank(currentPointInt);
                if (newRank > oldRank)
                {
                    var key =  ((currentPointInt / 100) * 100).ToString();
                    PlayFabReadClient.ArenaAwards.TryGetValue(key, out var award);
                    if (award != null)
                        awardDM = award.d;
                }
                
                if (PlayerAccountInfo.Me != null)
                {
                    success.Invoke(PlayerAccountInfo.Me.arenaPoint , currentPointInt, awardDM);
                    PlayerAccountInfo.Me.arenaPoint = currentPointInt;
                }
            }
        );
    }
}

public class LeaderboardInfo
{
    public PlayerLeaderboardEntry PlayerLeaderboardEntry;
    public MultiDic<int, int, UnitInfo>.SerializableSet[] Team;
    public string OneWord;
    public string plusPoint;
}

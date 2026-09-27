using PlayFab.ClientModels;
using System;
using ExecuteCloudScriptResult = PlayFab.ClientModels.ExecuteCloudScriptResult;

public partial class CloudScript
{
    /// <summary>
    /// claimQuestRewardSuccess基于的是claimQuestReward的结果而不是completedLevel
    /// </summary>
    /// <param name="stage"></param>
    /// <param name="claimQuestRewardSuccess"></param>
    public static void GangbangProgress(string stage, Action<ExecuteCloudScriptResult> claimQuestRewardSuccess)
    {
        // 之所以把更新关卡进度和获取报酬分开处理，是因为当时把这些处理写到一个cloud函数里的时候，
        // 竟然有一定概率playfab不给执行关卡进度更新所触发的角色获取rule，于是我们才决定在这个部分不要把各种处理集中在一个瞬间
        // 原本在cloudscript内会查询是不是第一次通某关，给去掉了也是这个原因。
        // 所以，如果玩家在更新了关卡的瞬间掉线，导致随后的获取报酬云函数没执行，完全可能获得不了这一关的报酬。但关卡进度更新了的话应该是能拿到对应的角色
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = "completedLevel",
                FunctionParameter = new
                {
                    stage = stage,
                    stageType = "gangbang"
                },
            },
            (x) =>
            {
                if (x.FunctionResult != null && !String.IsNullOrEmpty(x.FunctionResult.ToString()))
                {
                    var jsonResult = (PlayFab.Json.JsonObject)x.FunctionResult;
                }
                
                ExecuteCloudScriptMainSceneCommon(
                    new ExecuteCloudScriptRequest
                    {
                        FunctionName = "claimQuestReward",
                        FunctionParameter = new
                        {
                            stage = stage,
                            isVip = PlayerAccountInfo.Me.noAdsState,
                            stageType = "gangbang"
                        }
                    },
                    claimQuestRewardSuccess
                );
            }
        );
    }
}
using PlayFab.ClientModels;
using System;
using ExecuteCloudScriptResult = PlayFab.ClientModels.ExecuteCloudScriptResult;

public partial class CloudScript
{
    public static void EventBattleProgress(string stage, Action<ExecuteCloudScriptResult> claimQuestRewardSuccess)
    {
        // 之所以把更新关卡进度和获取报酬分开处理，是因为当时把这些处理写到一个cloud函数里的时候，
        // 竟然有一定概率playfab不给执行关卡进度更新所触发的角色获取rule，于是我们才决定在这个部分不要把各种处理集中在一个瞬间
        // 原本在cloudscript内会查询是不是第一次通某关，给去掉了也是这个原因。
        // 所以，如果玩家在更新了关卡的瞬间掉线，导致随后的获取报酬云函数没执行，完全可能获得不了这一关的报酬。但关卡进度更新了的话应该是能拿到对应的角色

        string levelKey;
        
        if (stage.Contains("easy"))
        {
            levelKey = "easy";
        }
        else if (stage.Contains("normal"))
        {
            levelKey = "normal";
        }
        else if (stage.Contains("hard"))
        {
            levelKey = "hard";
        }
        else
        {
            return;
        }
        
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = "checkAndUpdateEventBattleProgress",
                FunctionParameter = new
                {
                    eventBattleId = stage
                },
            },
            (x) =>
            {
                ExecuteCloudScriptMainSceneCommon(
                    new ExecuteCloudScriptRequest
                    {
                        FunctionName = "claimEventReward",
                        FunctionParameter = new
                        {
                            level = levelKey,
                        }
                    },
                    claimQuestRewardSuccess
                );
            }
        );
    }
}

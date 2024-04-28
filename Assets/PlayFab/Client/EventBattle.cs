using System;
using PlayFab;
using PlayFab.ClientModels;

public partial class PlayFabReadClient
{
    // 调用函数以获取已完成的关卡列表
    public static void GetCompletedLevels(Action<ExecuteCloudScriptResult> onSuccess) {
        ExecuteCloudScriptRequest request = new ExecuteCloudScriptRequest {
            FunctionName = "getCompletedEventBattle", // 必须与Cloud Script中的函数名匹配
            GeneratePlayStreamEvent = false // 可选，根据需要设置
        };
        PlayFabClientAPI.ExecuteCloudScript(request, onSuccess, ErrorReport);
    }
}

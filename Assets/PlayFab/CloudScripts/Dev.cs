using System;
using PlayFab.ClientModels;

public partial class CloudScript
{
    public static void Common(string functionName, Action<ExecuteCloudScriptResult> success, bool generatePlayStreamEvent = false)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest()
            {
                FunctionName = functionName, // Arbitrary function name (must exist in your uploaded cloud.js file)
                GeneratePlayStreamEvent = generatePlayStreamEvent, // Optional - Shows this event in PlayStream
            },
            success
        );
    }
}

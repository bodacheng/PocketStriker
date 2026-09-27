using System;
using PlayFab.ClientModels;

public partial class CloudScript
{
    public static void BoughtBundle(string productId, Action success)
    {
        ExecuteCloudScriptMainSceneCommon(
            new ExecuteCloudScriptRequest
            {
                FunctionName = "BundleBought",
                FunctionParameter = new
                {
                    bundleProductId = productId
                }
            },
            (x) =>
            {
                success.Invoke();
            }
        );
    }
}

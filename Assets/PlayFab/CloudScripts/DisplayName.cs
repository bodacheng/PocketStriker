using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public partial class CloudScript
{
    public static void UpdateUserTitleDisplayName(string displayName)
    {
        PlayFabClientAPI.UpdateUserTitleDisplayName(
            new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = displayName
            },
            result =>
            {
                Debug.Log("The player's display name is now: " + result.DisplayName);
            },
            PlayFabReadClient.ErrorReport
        );
    }
}

using PlayFab;
using PlayFab.ClientModels;
using System;

public partial class PlayFabReadClient
{
    public static void GetStatistics(Action<bool> finished)
    {
        void Attempt(int attempt)
        {
            PlayFabClientAPI.GetPlayerStatistics(
                new GetPlayerStatisticsRequest(),
                (GetPlayerStatisticsResult result) => {
                    OnGetStatistics(result);
                    finished(true);
                },
                error =>
                {
                    if (ShouldRetryPlayFabRequest(error, attempt))
                    {
                        RetryPlayFabRequest(() => Attempt(attempt + 1), attempt, "GetPlayerStatistics");
                        return;
                    }

                    finished.Invoke(false);
                    ErrorReport(error);
                }
            );
        }

        Attempt(1);
    }

    static void OnGetStatistics(GetPlayerStatisticsResult result)
    {
        foreach (StatisticValue value in result.Statistics)
        {
            if (value.StatisticName == PlayFabSetting._arenaPointCode)
            {
                PlayerAccountInfo.Me.arenaPoint = value.Value;
            }

            if (value.StatisticName == "stageProgress")
            {
                PlayerAccountInfo.Me.arcadeProcess = value.Value;
            }
            
            if (value.StatisticName == "gangbangProgress")
            {
                PlayerAccountInfo.Me.gangbangProcess = value.Value;
            }
        }
    }
}

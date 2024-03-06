using UnityEngine;
using PlayFab;
using PlayFab.AdminModels;
using System.Collections.Generic;
using Newtonsoft.Json;

public class TitleData : MonoBehaviour
{
    public class ArcadeReward
    {
        public int g;
        public int dia;
    }

    public static void SetArcadeRewards()
    {
        for (int i = 1; i < 101; i++)
        {
            SetTitleDataRequest request = new SetTitleDataRequest
            {
                Key = "stage_" + i
            };
            ArcadeReward arcadeReward = new ArcadeReward
            {
                dia = 1,
                g = 2
            };

            request.Value = JsonConvert.SerializeObject(arcadeReward);

            PlayFabAdminAPI.SetTitleInternalData(
                request,
                (SetTitleDataResult result) =>
                {
                    Debug.Log(result);
                },
                (PlayFabError PlayFabError) => {
                    Debug.Log(PlayFabError);
                }
            );
        }
    }
}

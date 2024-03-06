using System;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using Newtonsoft.Json;

public partial class PlayFabReadClient
{
    private static IDictionary<string, Award> _stageAward;
    public static IDictionary<string, Award> StageAwards => _stageAward;
    private static IDictionary<string, Award> _gangbangAward;
    public static IDictionary<string, Award> GangbangAwards => _gangbangAward;
    
    private static IDictionary<string, Award> _arenaAward;
    public static IDictionary<string, Award> ArenaAwards => _arenaAward;

    public static TimeLimitedBuyData TimeLimitedBuyData;
    
    public static void GetAllTitleData(Action<bool> finished)
    {
        PlayFabClientAPI.GetTitleData(
            new GetTitleDataRequest
            {
                Keys = new List<string>() {"stage_awards", "gangbang_awards","arena_awards", PlayFabSetting._timeLimitBuyCode}
            },
            result =>
            {
                #region adventure
                
                var stageAwardObject = result.Data["stage_awards"];
                var stageAwards = JsonConvert.DeserializeObject<List<StageAward>>(stageAwardObject);
                _stageAward = new Dictionary<string, Award>();
                foreach (var kv in stageAwards)
                {
                    if (!_stageAward.ContainsKey(kv.stageKey))
                    {
                        _stageAward.Add(kv.stageKey, kv.award);
                    }
                }
                
                #endregion

                #region gangbang
                
                var gangbangAwardObject = result.Data["gangbang_awards"];
                var gangbangAwards = JsonConvert.DeserializeObject<List<StageAward>>(gangbangAwardObject);
                _gangbangAward = new Dictionary<string, Award>();
                foreach (var kv in gangbangAwards)
                {
                    if (!_gangbangAward.ContainsKey(kv.stageKey))
                    {
                        _gangbangAward.Add(kv.stageKey, kv.award);
                    }
                }
                
                #endregion

                #region arena

                var arenaAwardObject = result.Data["arena_awards"];
                var arenaAwards = JsonConvert.DeserializeObject<List<StageAward>>(arenaAwardObject);
                _arenaAward = new Dictionary<string, Award>();
                foreach (var kv in arenaAwards)
                {
                    if (!_arenaAward.ContainsKey(kv.stageKey))
                    {
                        _arenaAward.Add(kv.stageKey, kv.award);
                    }
                }
                #endregion
                
                #region time limit buy
                
                if (result.Data.ContainsKey(PlayFabSetting._timeLimitBuyCode))
                {
                    string specialBuyJson = result.Data[PlayFabSetting._timeLimitBuyCode];
                    TimeLimitedBuyData = JsonConvert.DeserializeObject<TimeLimitedBuyData>(specialBuyJson);
                }
                
                #endregion
                
                finished.Invoke(true);
            },
            (x) =>
            {
                finished(false);
                ErrorReport(x);
            }
        );
    }
}

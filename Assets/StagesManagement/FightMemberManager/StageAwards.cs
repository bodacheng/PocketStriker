#if UNITY_EDITOR
using System.Collections.Generic;
using Json;
using Newtonsoft.Json;

public partial class StageEditor
{
    public static void ExportStageAward()
    {
        var stageAwards = new List<StageAward>();
        for (int i = 1; i <= 150 ; i++)
        {
            var award = new StageAward
            {
                stageKey = i.ToString(),
                award = new Award
                {
                    g = 10,
                    d = i % 5 == 0 ? 10 : 5
                }
            };
            stageAwards.Add(award);
        }
        
        var json = JsonConvert.SerializeObject(stageAwards.ToArray());
        LocalJson.SaveInfoToJsonFile_dataPath(null, "stage_awards.json", json);
    }
}
#endif
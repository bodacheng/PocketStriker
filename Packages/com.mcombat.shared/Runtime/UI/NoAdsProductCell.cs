using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UI;

public class NoAdsProductCell : MonoBehaviour
{
    [SerializeField] private Text rewardedAdDMCount;
    [SerializeField] private Text extraText;
    
    void Start()
    {
        RequestAdRewards();
    }
    
    public void RequestAdRewards()
    {
        // 执行云脚本
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "GetSkippedAdRewards", // 云脚本的函数名
        }, OnCloudScriptSuccess, OnCloudScriptFailure);
    }

    void OnCloudScriptSuccess(ExecuteCloudScriptResult result)
    {
        // 检查返回值并处理
        if (result.FunctionResult != null)
        {
            var jsonResult = (PlayFab.Json.JsonObject)result.FunctionResult;
            // 将FunctionResult解析为JSON对象
            var resultDic = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(PlayFab.Json.PlayFabSimpleJson.SerializeObject(jsonResult));
        
            // 从JSON对象中提取reward值
            if (resultDic.ContainsKey("rewardDM"))
            {
                int reward = Convert.ToInt32(resultDic["rewardDM"]);
                Debug.Log("Received ad reward: " + reward);
                if (reward > 0)
                {
                    rewardedAdDMCount.text = reward.ToString();
                    extraText.text = Translate.Get("skippedAdRewardsIntro");
                    rewardedAdDMCount.gameObject.SetActive(true);
                    extraText.gameObject.SetActive(true);
                }
            }
            else
            {
                Debug.LogError("Reward not found in cloud script result");
            }
        }
        else
        {
            Debug.LogError("No result received from cloud script");
        }
    }

    void OnCloudScriptFailure(PlayFabError error)
    {
        Debug.LogError("Failed to execute cloud script: " + error.GenerateErrorReport());
    }
}

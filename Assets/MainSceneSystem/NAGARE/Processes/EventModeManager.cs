using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using PlayFab.ClientModels;
using PlayFab.Json;
using UnityEngine;

public class EventModeManager
{
    private IResourceLocation easyModePath, normalModePath, hardModePath;
    private FightInfo easyMode, normalMode, hardMode;
    private readonly StageModeTable _stageModeTable = new StageModeTable();
    private List<string> completedLevels;

    public List<string> CompletedLevels
    {
        get
        {
            if (completedLevels != null)
                return completedLevels;
            else
            {
                completedLevels = new List<string>();
                return completedLevels;
            }
        }
        set => completedLevels = value;
    }
    
    /// <summary>
    /// PrimaryKey是用来定位到底哪个战斗文件是哪个难度级别战斗的，
    /// boss关卡进度的实际索引靠的是战斗定义文件本身。所以PrimaryKey可以常年不变
    /// </summary>
    public async UniTask Initialize()
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync("event_stage");
        await locationHandle.Task;
        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var stageLocation in locationHandle.Result)
            {
                if (stageLocation.PrimaryKey.Contains("easy"))
                {
                    easyModePath = stageLocation;
                }
                if (stageLocation.PrimaryKey.Contains("normal"))
                {
                    normalModePath = stageLocation;
                }
                if (stageLocation.PrimaryKey.Contains("hard"))
                {
                    hardModePath = stageLocation;
                }
            }
        }
        Addressables.Release(locationHandle);
        await _stageModeTable.LoadStageMode();

        if (easyModePath != null)
            easyMode = await LoadStage(easyModePath);
        if (normalModePath != null)
            normalMode = await LoadStage(normalModePath);
        if (hardModePath != null)
            hardMode = await LoadStage(hardModePath);
    }

    public UnitInfo GetRepresentativeUnit()
    {
        var unit1 = hardMode?.UnitsData.FirstOrDefault();
        if (unit1 != null) return unit1;
        var unit2 = normalMode?.UnitsData.FirstOrDefault();
        if (unit2 != null) return unit2;
        var unit3 = easyMode?.UnitsData.FirstOrDefault();
        if (unit3 != null) return unit3;
        return null;
    }
    
    async UniTask<FightInfo> LoadStage(IResourceLocation location)
    {
        var fightInfo = await AddressablesLogic.LoadT<FightInfo>(location);
        fightInfo.EventType = FightEventType.Event;
        fightInfo.ArcadeFightMode = _stageModeTable.GetModeById(fightInfo.ID);
        fightInfo.FightMembers.SetEnemyLevel(fightInfo.stageRefLevel);
        return fightInfo;
    }
    
    public void OnCloudScriptSuccess(ExecuteCloudScriptResult result, EventBattleTop layer)
    {
        if (result.Error != null) {
            Debug.LogError("Cloud Script Error: " + result.Error.Message);
            return;
        }
        
        Debug.Log("Cloud Script Success: " + result.FunctionResult);
        JsonObject jsonResult = (JsonObject)result.FunctionResult;
        if (jsonResult.TryGetValue("completedEventBattles", out var completedBattlesObject))
        {
            var objects = (List<object>)completedBattlesObject;
            CompletedLevels.Clear();
            foreach (var o in objects)
            {
                CompletedLevels.Add(o.ToString());
            }
        }
        else
        {
            Debug.Log("No completed event battles found.");
            return;
        }
        
        if (easyModePath != null)
        {
            layer.EasyModeBtn.Setup(() =>
            {
                PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, easyMode, true);
            }, PlayFabReadClient.EventAwards["easy"],  CompletedLevels.Contains(easyMode.ID), easyMode.team2CGMode);
        }
        
        if (normalModePath != null)
        {
            layer.NormalModeBtn.Setup(() =>
            {
                PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, normalMode, true);
            }, PlayFabReadClient.EventAwards["normal"],CompletedLevels.Contains(normalMode.ID), normalMode.team2CGMode);
        }
        
        if (hardModePath != null)
        {
            layer.HardModeBtn.Setup(() =>
            {
                PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, hardMode, true);
            }, PlayFabReadClient.EventAwards["hard"],CompletedLevels.Contains(hardMode.ID), hardMode.team2CGMode);
        }
    }
}

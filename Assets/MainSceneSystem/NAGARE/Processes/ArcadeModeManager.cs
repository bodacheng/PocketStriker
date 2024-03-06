using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class ArcadeModeManager
{
    private readonly IDictionary<string, IResourceLocation> locationKeyDic = new Dictionary<string, IResourceLocation>();
    private readonly StageModeTable _stageModeTable = new StageModeTable();
    int _maxStageNum = -999;
    public int MaxStageNum => _maxStageNum;
    public async UniTask Initialize()
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync("quest");
        await locationHandle.Task;
        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var stageLocation in locationHandle.Result)
            {
                DicAdd<string, IResourceLocation>.Add(locationKeyDic, stageLocation.PrimaryKey, stageLocation);
                int id = Int32.Parse(stageLocation.PrimaryKey);
                if (id > _maxStageNum)
                {
                    _maxStageNum = id;
                }
            }
        }
        Addressables.Release(locationHandle);
        await _stageModeTable.LoadStageMode();
    }
    
    public async UniTask<FightInfo> LoadStage(int stageNo)
    {
        locationKeyDic.TryGetValue(stageNo.ToString(), out var location);
        if (location == null)
            return null;
        var fightInfo = await AddressablesLogic.LoadT<FightInfo>(location);
        fightInfo.EventType = FightEventType.Quest;
        fightInfo.ArcadeFightMode = _stageModeTable.GetModeById(fightInfo.ID);
        fightInfo.FightMembers.SetEnemyLevel(fightInfo.stageRefLevel);
        return fightInfo;
    }

    public async void DirectToArcadeStage(int stageNo, bool forward)
    {
        var stage = await LoadStage(stageNo);
        if (stage == null)
        {
            stage = await LoadStage(stageNo - 1);
        }
        stage.EventType = FightEventType.Quest;
        PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, stage, forward);
    }
}

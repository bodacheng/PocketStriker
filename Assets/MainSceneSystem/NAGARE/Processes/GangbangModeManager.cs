using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class GangbangModeManager
{
    private readonly IDictionary<string, IResourceLocation> locationKeyDic = new Dictionary<string, IResourceLocation>();
    int _maxStageNum = -999;
    public int MaxStageNum => _maxStageNum;
    public async UniTask Initialize()
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync("quest_gangbang");
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
    }
    
    public async UniTask<GangbangInfo> LoadStage(int stageNo)
    {
        locationKeyDic.TryGetValue(stageNo.ToString(), out var location);
        if (location == null)
            return null;
        var fightInfo = await AddressablesLogic.LoadT<GangbangInfo>(location);
        fightInfo.EventType = FightEventType.Gangbang;
        fightInfo.ArcadeFightMode = 1;
        fightInfo.FightMembers.SetEnemyLevel(fightInfo.stageRefLevel);
        return fightInfo;
    }

    public async void DirectToGangStage(int stageNo, bool forward)
    {
        var stage = await LoadStage(stageNo);
        if (stage == null)
        {
            stage = await LoadStage(stageNo - 1);
        }
        stage.EventType = FightEventType.Gangbang;
        PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, stage, forward);
    }
}

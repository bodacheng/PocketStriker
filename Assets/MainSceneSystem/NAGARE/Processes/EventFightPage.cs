using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class EventFightPage : MSceneProcess
{
    private IResourceLocation easyModePath, normalModePath, hardModePath;
    private FightInfo easyMode, normalMode, hardMode;
    private readonly StageModeTable _stageModeTable = new StageModeTable();
    
    public EventFightPage()
    {
        Step = MainSceneStep.EventFight;
    }
    
    public override void ProcessEnter()
    {
        
    }
    
    public async UniTask Initialize()
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync("quest");
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
    }
    
    public async UniTask<FightInfo> LoadStage(IResourceLocation location)
    {
        var fightInfo = await AddressablesLogic.LoadT<FightInfo>(location);
        fightInfo.EventType = FightEventType.Event;
        fightInfo.ArcadeFightMode = _stageModeTable.GetModeById(fightInfo.ID);
        fightInfo.FightMembers.SetEnemyLevel(fightInfo.stageRefLevel);
        return fightInfo;
    }
}

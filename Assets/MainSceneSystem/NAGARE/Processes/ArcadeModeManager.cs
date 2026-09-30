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
    
    public static readonly ArcadeModeManager Instance = new ArcadeModeManager();
    
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
        var stageAsset = await AddressablesLogic.LoadT<FightInfo>(location);
        if (stageAsset == null)
            return null;

        return PrepareStage(stageAsset, stageNo, _stageModeTable.GetModeById(stageNo.ToString()));
    }

    /// <summary>Creates an owned stage visit from authored data without loading any resources.</summary>
    public static FightInfo PrepareStage(FightInfo stageAsset, int stageNo, int configuredMode)
    {
        if (stageAsset == null) return null;

        // Team selection and evolution generation belong to this visit, not the
        // Addressables asset shared by the stage list and later retries.
        var fightInfo = stageAsset is GangbangInfo group
            ? GangbangInfo.Copy(group) : UnityEngine.Object.Instantiate(stageAsset);
        fightInfo.name = stageAsset.name;
        fightInfo.OpenAndSetEnemyDataOnPlace();
        fightInfo.ID = stageNo.ToString();
        fightInfo.EventType = FightEventType.Quest;
        fightInfo.ArcadeFightMode = AdventureModeRules.ResolveMode(
            fightInfo.ID, configuredMode);
        fightInfo.EvolutionMode = fightInfo.ArcadeFightMode == AdventureModeRules.EvolutionMode;
        var teamMode = fightInfo.IsGroupBattle || fightInfo.ArcadeFightMode == AdventureModeRules.MultiMode
            ? TeamMode.MultiRaid
            : TeamMode.Rotation;
        fightInfo.team1Mode = teamMode;
        fightInfo.team2Mode = teamMode;
        // Authored stages can have sparse IDs; evolution may add a new enemy at
        // one of those IDs. Keep preview and combat lookups unique per encounter.
        if (!fightInfo.IsGroupBattle)
            for (var index = 0; index < fightInfo.UnitsData.Count; index++)
                fightInfo.UnitsData[index].id = index.ToString();
        fightInfo.SetUnitLevelByRefLevel();
        return fightInfo;
    }

    public async void DirectToArcadeStage(int stageNo, bool forward)
    {
        var stage = await LoadStage(stageNo);
        if (stage == null)
        {
            stage = await LoadStage(stageNo - 1);
        }
        if (stage != null)
        {
            stage.EventType = FightEventType.Quest;
            PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, stage, forward);
        }
        else
        {
            PreScene.target.trySwitchToStep(MainSceneStep.ArcadeFront, forward);
        }
    }
}

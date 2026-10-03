using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using PlayFab.ClientModels;
using UnityEngine;

public class EventModeManager
{
    private IResourceLocation easyModePath, normalModePath, hardModePath;
    private FightInfo easyMode, normalMode, hardMode;
    private readonly StageModeTable _stageModeTable = new StageModeTable();
    private List<string> completedLevels;
    private readonly Dictionary<string, float> stageRefLevelCache = new Dictionary<string, float>();
    private readonly List<FightInfo> randomStages = new List<FightInfo>();

    public FightInfo EasyMode => easyMode;
    public FightInfo NormalMode => normalMode;
    public FightInfo HardMode => hardMode;
    
    public static readonly EventModeManager Instance = new EventModeManager();

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

    public async UniTask InitializeRandomMode(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId))
            throw new ArgumentException("A daily random Boss identifier is required.", nameof(uniqueId));

        var easyLevel = await GetStageRefLevel("easy", 2f);
        var normalLevel = await GetStageRefLevel("normal", 5f);
        var hardLevel = await GetStageRefLevel("hard", 10f);
        var generated = new List<FightInfo>();
        try
        {
            generated.Add(RandomBossStageFactory.Create("easy_" + uniqueId, CriticalGaugeMode.Normal, 3, easyLevel));
            generated.Add(RandomBossStageFactory.Create("normal_" + uniqueId, CriticalGaugeMode.DoubleGain, 2, normalLevel));
            generated.Add(RandomBossStageFactory.Create("hard_" + uniqueId, CriticalGaugeMode.Unlimited, 1, hardLevel));
        }
        catch
        {
            foreach (var stage in generated)
                UnityEngine.Object.Destroy(stage);
            throw;
        }

        // Re-entering rerolls opponents, while the server date keeps daily rewards stable.
        foreach (var previous in randomStages)
            UnityEngine.Object.Destroy(previous);
        randomStages.Clear();
        randomStages.AddRange(generated);
        easyMode = generated[0];
        normalMode = generated[1];
        hardMode = generated[2];
    }

    async UniTask<float> GetStageRefLevel(string address, float fallback)
    {
        if (stageRefLevelCache.TryGetValue(address, out var cachedLevel))
            return cachedLevel;

        AsyncOperationHandle<FightInfo> handle = default;
        var level = fallback;
        try
        {
            handle = Addressables.LoadAssetAsync<FightInfo>(address);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null
                && handle.Result.stageRefLevel > 0f && !float.IsInfinity(handle.Result.stageRefLevel))
            {
                level = handle.Result.stageRefLevel;
                stageRefLevelCache[address] = level;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RandomBoss] Unable to read '{address}' difficulty; using level {fallback}. {exception.Message}");
        }
        finally
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        return level;
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
        fightInfo.ApplyBattleModeRules();
        fightInfo.SetUnitLevelByRefLevel();
        return fightInfo;
    }
    
    public void OnCloudScriptSuccess(ExecuteCloudScriptResult result, EventBattleTop layer)
    {
        if (!TryReadCompletedLevels(result))
            return;
        
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

    public bool TryReadCompletedLevels(ExecuteCloudScriptResult result)
    {
        if (result == null || result.Error != null)
        {
            Debug.LogWarning("[RandomBoss] Unable to read completed battles: " + result?.Error?.Message);
            return false;
        }

        try
        {
            var payload = CloudScriptPayloadUtility.Deserialize<CompletedLevelsResponse>(result.FunctionResult);
            if (payload?.completedEventBattles == null)
            {
                Debug.LogWarning("[RandomBoss] The completed-battles response was missing its list.");
                return false;
            }
            CompletedLevels = payload.completedEventBattles.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[RandomBoss] Invalid completed-battles response: " + exception.Message);
            return false;
        }
    }

    public void SetupRandomMode(EventBattleTop layer)
    {
        if (layer == null)
            return;
        SetupRandomButton(layer.EasyModeBtn, easyMode, "easy");
        SetupRandomButton(layer.NormalModeBtn, normalMode, "normal");
        SetupRandomButton(layer.HardModeBtn, hardMode, "hard");
    }

    void SetupRandomButton(EventBattleButton button, FightInfo stage, string difficulty)
    {
        if (button == null)
            return;
        var awards = PlayFabReadClient.EventAwards;
        if (stage == null || awards == null || !awards.TryGetValue(difficulty, out var award) || award == null)
        {
            button.gameObject.SetActive(false);
            Debug.LogWarning("[RandomBoss] Missing stage or reward data for " + difficulty);
            return;
        }

        button.Setup(
            () => PreScene.target.trySwitchToStep(MainSceneStep.QuestInfo, stage, true),
            award, CompletedLevels.Contains(stage.ID), stage.team2CGMode);
    }

    sealed class CompletedLevelsResponse
    {
        public List<string> completedEventBattles;
    }
}

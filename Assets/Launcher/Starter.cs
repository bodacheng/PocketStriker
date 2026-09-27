using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IngameDebugConsole;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
[CustomEditor(typeof(Starter))]
public class StarterGUI : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var s = (Starter)target;
        if (GUILayout.Button("Refresh"))
        {
            Refresh(s);
        }
    }

    async void Refresh(Starter starter)
    {
        await AddressablesLogic.DownLoadConfig();
        CommonSetting commonSetting = await AddressablesLogic.GetCommonSetting();
        commonSetting.Initialise();
        await starter.Initialise();
    }
}
#endif

[ExecuteInEditMode]
public class Starter : MonoBehaviour
{
    [SerializeField] PlayFabSetting playFabSetting;
    [SerializeField] DefaultIconSetting defaultIconSetting;
    [SerializeField] DebugLogManager inGameDebugConsole;
    
    public static bool ConfigInitialised = false;
    
    public async UniTask Initialise()
    {
        ConfigInitialised = false;
        AddressablesLogic.ReleaseAsyncOperationHandles();
        inGameDebugConsole.gameObject.SetActive(CommonSetting.DevMode);
        playFabSetting.Initialise();
        defaultIconSetting.Initialise();
        await UniTask.WhenAll(
            new List<UniTask>()
            {
                SkillConfigTable.LoadAllSkillConfigs(),
                PowerEstimateTable.LoadFile(),
                Units.LoadUnitConfigs(),
                Translate.LoadLanguageCodes(LoadRequiredConfiguration<TextAsset>),
                ShortStory.LoadLanguageCodes(LoadRequiredConfiguration<TextAsset>),
                Story.LoadLanguageCodes(LoadRequiredConfiguration<TextAsset>),
                UnitPassiveTable.Load(
                    LoadRequiredConfiguration<TextAsset>,
                    skillId => SkillConfigTable.GetSkillConfigByRecordId(skillId) != null),
                FightGlobalSetting.LoadFightParams(LoadRequiredConfiguration<FightGlobalSetting>)
            }
        );
        //MobileAds.Initialize(initStatus => { Debug.Log(initStatus);});
        ConfigInitialised = true;
    }

    static async UniTask<T> LoadRequiredConfiguration<T>(string key) where T : UnityEngine.Object
    {
        var asset = await AddressablesLogic.LoadT<T>(key);
        if (asset == null)
            throw new System.InvalidOperationException($"Required configuration is missing: {key}");
        return asset;
    }
    
    public void EnterFrontScene()
    {
        var stage = FightInfo.ScreenSaverStage(TeamMode.Rotation);
        stage.EventType = FightEventType.Screensaver;
        FightLoad.Go(stage);
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class StartUpPresentation : MonoBehaviour
{
    enum StartupStage
    {
        VersionCheck,
        Configuration,
        ConfigurationInitialization,
        ResourceInspection,
        DownloadUi,
        ResourceDownload
    }

    [SerializeField] Starter starter;
    [FormerlySerializedAs("t")]
    [SerializeField] RectTransform safeAreaRect;
    [SerializeField] bool frontSceneFight;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioSource uiAudioSource;
    [SerializeField] Canvas canvas;
    StartupStage startupStage;
    bool startupInProgress;
    
    void OpenAppStoreLink()
    {
        string storeLink = "";
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            storeLink = "https://apps.apple.com/app/idYOUR_APP_ID";
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            storeLink = "https://play.google.com/store/apps/details?id=YOUR_PACKAGE_NAME";
        }

        if (!string.IsNullOrEmpty(storeLink))
        {
            Application.OpenURL(storeLink);
        }
        else
        {
            Debug.LogError("Unable to open App Store link.");
        }
    }
    
    void Start()
    {
        var safeAreaRoot = safeAreaRect != null ? safeAreaRect : canvas.GetComponent<RectTransform>();
        PosCal.Canvas = this.canvas;
        PosCal.SafeAreaRect = safeAreaRoot;
        PosCal.TestIni();
        safeAreaRoot = PosCal.SafeAreaRect;
        UILayerLoader.SetHanger(safeAreaRoot, canvas.transform);
        PocketStrikerAppSettings.Load();
        AppSetting.BGMSource = audioSource;
        AppSetting.BGMSource.volume = AppSetting.Value.BgmVolume;
        AppSetting.UiAudioSource = uiAudioSource;
        AppSetting.UiAudioSource.volume = AppSetting.Value.EffectsVolume;
        OnStart().Forget();
    }
    
    async UniTask OnStart()
    {
        try
        {
            await PrepareStartup();
        }
        catch (Exception exception)
        {
            ShowResourceFailure(exception);
        }
    }

    void ShowResourceFailure(Exception exception)
    {
        Debug.LogError($"[Startup] {startupStage} failed: {exception}", this);
        ProgressLayer.Close();
        string message;
        if (startupStage == StartupStage.ConfigurationInitialization)
        {
            message = AppSetting.Value.Language switch
            {
                SystemLanguage.Japanese => "ゲーム設定の初期化に失敗しました。再試行してください。",
                SystemLanguage.Chinese => "游戏配置初始化失败，请重试。",
                _ => "Game configuration initialization failed. Please try again."
            };
        }
        else if (startupStage == StartupStage.DownloadUi)
        {
            message = AppSetting.Value.Language switch
            {
                SystemLanguage.Japanese => "ダウンロード画面の初期化に失敗しました。再試行してください。",
                SystemLanguage.Chinese => "下载界面初始化失败，请重试。",
                _ => "Download screen initialization failed. Please try again."
            };
        }
        else
        {
            message = AppSetting.Value.Language switch
            {
                SystemLanguage.Japanese => "起動に必要なリソースを準備できませんでした。再試行してください。",
                SystemLanguage.Chinese => "启动资源准备失败，请重试。",
                _ => "Could not prepare startup resources. Please try again."
            };
        }
        PopupLayer.ArrangeWarnWindow(() => SceneManager.LoadScene(0), message);
    }

    void ShowInitializationFailure(Exception exception)
    {
        Debug.LogError($"[Startup] GameInitialization failed: {exception}", this);
        ProgressLayer.Close();
        var message = AppSetting.Value.Language switch
        {
            SystemLanguage.Japanese => "ゲームの初期化に失敗しました。再試行してください。",
            SystemLanguage.Chinese => "游戏初始化失败，请重试。",
            _ => "Game initialization failed. Please try again."
        };
        PopupLayer.ArrangeWarnWindow(() => SceneManager.LoadScene(0), message);
    }

    async UniTask PrepareStartup()
    {
        string text = String.Empty;
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.English:
                text = "Checking program version...";
                break;
            case SystemLanguage.Japanese:
                text = "プログラムのバージョンを確認中...";
                break;
            case SystemLanguage.Chinese:
                text = "正在检测程序版本";
                break;
        }
        
        ProgressLayer.Loading(text);
        startupStage = StartupStage.VersionCheck;
        bool needToUpdate = await AddressablesLogic.VersionConfirm();
        ProgressLayer.Close();
        
        if (needToUpdate)
        {
            switch (AppSetting.Value.Language)
            {
                case SystemLanguage.English:
                    text = "New version detected, please update the program";
                    break;
                case SystemLanguage.Japanese:
                    text = "新しいバージョンが検出されました。プログラムをアップデートしてください";
                    break;
                case SystemLanguage.Chinese:
                    text = "监测到新版本，请更新程序";
                    break;
                default:
                    text = "New version detected, please update the program";
                    break;
            }
            PopupLayer.ArrangeWarnWindow(() =>
            {
                #if UNITY_ANDROID
                Application.OpenURL("https://play.google.com/store/apps/details?id=com.MCombat.BO");
                #endif
                
                #if UNITY_IOS
                Application.OpenURL("https://apps.apple.com/app/pocket-striker/id6478905824");
                #endif
            }, text);
            return;
        }
        
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.English:
                text = "Inspecting resources";
                break;
            case SystemLanguage.Japanese:
                text = "リソースを検査中";
                break;
            case SystemLanguage.Chinese:
                text = "检查资源中";
                break;
        }
        ProgressLayer.Loading(text);
        
        // 告诉用户检查资源中，其实也把config文件下载了，合起来几十kb而已。
        startupStage = StartupStage.Configuration;
        await AddressablesLogic.DownLoadConfig();
        CommonSetting commonSetting = await AddressablesLogic.GetCommonSetting();
        startupStage = StartupStage.ConfigurationInitialization;
        commonSetting.Initialise();
        
        startupStage = StartupStage.ResourceInspection;
        string failedLabel = null;
        var bytes = await AddressablesLogic.GetWholeDownLoadSize(
            label => failedLabel = label,
            commonSetting.DownLoadLabels
        );
        if (failedLabel != null)
        {
            throw new InvalidOperationException($"Failed to inspect required resources: {failedLabel}");
        }
        
        ProgressLayer.Close();
        if (bytes > 0)
        {
            DownLoadConfirm("Download Size :" + Math.Round((double)bytes / 1048576, 1) + "MB" + "\n\n" + "Start to download", 
                bytes, commonSetting.DownLoadLabels);
        }
        else
        {
            await Go();
        }
    }
    
    void DownLoadConfirm(string msg, float wholeBytes, List<string> downLoadLabels)
    {
        PopupLayer.ArrangeConfirmWindow(
            () => DownloadAndStart(wholeBytes, downLoadLabels).Forget(),
            Application.Quit,
            msg
        );
    }

    async UniTask DownloadAndStart(float wholeBytes, List<string> downLoadLabels)
    {
        try
        {
            startupStage = StartupStage.DownloadUi;
            HighLightLayer.Close();
            // Download UI uses only the bundled progress layer, with no character art.
            ProgressLayer.Downloading(AddressablesResourcePolicy.DownloadProgressText(AppSetting.Value.Language));
            startupStage = StartupStage.ResourceDownload;
            await AddressablesLogic.ResourcePrepareProcess(
                null,
                progress => ProgressLayer.LoadingPercent(progress, AddressablesLogic.DownloadedBytes / wholeBytes),
                downLoadLabels
            );
            ProgressLayer.Close();
            await Go();
        }
        catch (Exception exception)
        {
            ShowResourceFailure(exception);
        }
    }

    async UniTask Go()
    {
        if (startupInProgress)
            return;

        startupInProgress = true;
        try
        {
            HighLightLayer.Close();
            Application.targetFrameRate = 70;
            FightGlobalSetting.SceneStep = 1;
            var text = AppSetting.Value.Language switch
            {
                SystemLanguage.Japanese => "ゲームデータを初期化中...",
                SystemLanguage.Chinese => "正在初始化游戏数据...",
                _ => "Initializing game data..."
            };
            ProgressLayer.Loading(text);
            await UniTask.NextFrame();

            await starter.Initialise();
            if (frontSceneFight && PlayFabReadClient.DontShowFrontFight == "False")
            {
                ProgressLayer.Close();
                starter.EnterFrontScene();
            }
            else
            {
                await AppSetting.PlayBGM(CommonSetting.StartThemeAddressKey);
                var titleBgLayer = UILayerLoader.Load<TitleBgLayer>(true, null, true);
                await titleBgLayer.Setup(1);
                titleBgLayer.Rotate(false);
                var titleScreenLayer = UILayerLoader.Load<TitleScreenLayer>(true, null, true);
                titleScreenLayer.Initialise();
                ProgressLayer.Close();
            }
        }
        catch (Exception exception)
        {
            ShowInitializationFailure(exception);
        }
        finally
        {
            startupInProgress = false;
        }
    }
}

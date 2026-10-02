using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using mainMenu;
using UnityEngine;
using FightScene;
using DummyLayerSystem;

public static class FightLoad
{
    const int FightSceneBuildIndex = 2;
    public const float SceneLoadingProgressEnd = 0.12f;
    static bool sceneLoadInProgress;

    public static FightInfo Fight;

    public static void ConfigureBattleControl(FightInfo fightInfo)
    {
        if (fightInfo == null) return;
        fightInfo.Team1Auto = fightInfo.ShouldForceAutoBattle || PlayerPrefs.GetInt("auto", 0) == 1;
        fightInfo.Team2Auto = true;
        fightInfo.RunTutorial = fightInfo.ShouldRunFirstQuestTutorial;
        if (fightInfo.RunTutorial)
        {
            fightInfo.Team1Auto = false;
            fightInfo.Team2Auto = false;
        }
    }

    public static void Go(FightInfo fightInfo, bool inSceneLoad = false)
    {
        if (fightInfo == null || (!inSceneLoad && sceneLoadInProgress))
            return;
        ConfigureBattleControl(fightInfo);

        Fight = fightInfo is GangbangInfo gangbangInfo
            ? GangbangInfo.Copy(gangbangInfo)
            : FightInfo.Copy(fightInfo);

        if (!inSceneLoad)
        {
            // Releasing the lobby clips resets any still-visible preview to its
            // bind pose. Hide the complete preparation hierarchy (including its
            // model cameras) before clearing resources or fading in loading UI.
            UILayerLoader.Remove<FightPrepareLayer>();
            PreScene.CashClear();
            LoadFightSceneAsync().Forget();
        }
        else
        {
            FSceneProcessesRunner.Main.ChangeProcess(SceneStep.Preparing);
        }
    }


    static async UniTaskVoid LoadFightSceneAsync()
    {
        if (sceneLoadInProgress)
        {
            return;
        }

        sceneLoadInProgress = true;
        var loadingBattleText = Translate.Get("LoadingBattle");
        ProgressLayer.Loading(loadingBattleText);
        ProgressLayer.LoadingPercent(loadingBattleText, 0f, false);

        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            var operation = SceneManager.LoadSceneAsync(FightSceneBuildIndex);
            if (operation == null)
            {
                SceneManager.LoadScene(FightSceneBuildIndex);
                return;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                var sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                ProgressLayer.LoadingPercent(loadingBattleText, Mathf.Lerp(0f, SceneLoadingProgressEnd, sceneProgress), false);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            ProgressLayer.LoadingPercent(loadingBattleText, SceneLoadingProgressEnd, false);
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
        finally
        {
            sceneLoadInProgress = false;
        }
    }
}

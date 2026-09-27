#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DummyLayerSystem;
using FightScene;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>Opt-in standalone development-player verification without account actions.</summary>
public sealed class PocketStrikerPlayerSmoke : MonoBehaviour
{
    const string SmokeArgument = "-pocketstriker-smoke";
    const string ReportArgument = "-smokeReport";
    const double TimeoutSeconds = 120;
    const double ReadySeconds = 20;

    readonly Stopwatch elapsed = new Stopwatch();
    readonly List<string> errors = new List<string>();
    string reportPath;
    double readySince = -1;
    bool requestedFight;
    bool finished;

    [Serializable]
    sealed class Report
    {
        public string check = "standalone-startup";
        public string unityVersion;
        public string platform;
        public bool passed;
        public double elapsedSeconds;
        public string state;
        public string[] errors;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (Array.IndexOf(arguments, SmokeArgument) < 0) return;

        Application.runInBackground = true;
        var runner = new GameObject(nameof(PocketStrikerPlayerSmoke)).AddComponent<PocketStrikerPlayerSmoke>();
        DontDestroyOnLoad(runner.gameObject);
        runner.reportPath = Path.Combine(Application.persistentDataPath, "pocketstriker-smoke-report.json");
        var reportIndex = Array.IndexOf(arguments, ReportArgument);
        if (reportIndex >= 0)
        {
            if (reportIndex + 1 >= arguments.Length || !Path.IsPathRooted(arguments[reportIndex + 1]))
                runner.errors.Add("-smokeReport requires an absolute file path.");
            else
                runner.reportPath = arguments[reportIndex + 1];
        }

        runner.elapsed.Start();
        Application.logMessageReceived += runner.CaptureError;
        Debug.Log("POCKETSTRIKER_PLAYER_SMOKE_STARTED report=" + runner.reportPath);
    }

    void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (errors.Count < 20) errors.Add(message + "\n" + stack);
    }

    void Update()
    {
        if (finished) return;
        if (errors.Count > 0)
        {
            Finish();
            return;
        }
        if (elapsed.Elapsed.TotalSeconds >= TimeoutSeconds)
        {
            errors.Add("Startup did not sustain the screensaver battle and title UI for 20 seconds within 120 seconds.");
            Finish();
            return;
        }

        var title = UILayerLoader.Get<TitleScreenLayer>();
        var scene = SceneManager.GetActiveScene();
        if (Starter.ConfigInitialised && title != null && scene.buildIndex == 0 && !requestedFight)
        {
            var starter = UnityEngine.Object.FindAnyObjectByType<Starter>();
            if (starter != null)
            {
                requestedFight = true;
                starter.EnterFrontScene();
            }
            return;
        }

        var manager = RTFightManager.Target;
        var ready = Starter.ConfigInitialised
            && scene.name == "FightScene"
            && FightLoad.Fight != null && FightLoad.Fight.EventType == FightEventType.Screensaver
            && FSceneProcessesRunner.Main.currentProcess is FightingProcess
            && manager != null && manager.team1 != null && manager.team2 != null
            && manager.team1.teamMembers.GetValues().Count > 0
            && manager.team2.teamMembers.GetValues().Count > 0
            && title != null && title.gameObject.activeInHierarchy;
        if (!ready)
        {
            readySince = -1;
            return;
        }

        if (readySince < 0)
        {
            readySince = elapsed.Elapsed.TotalSeconds;
            Debug.Log("POCKETSTRIKER_PLAYER_SMOKE_READY " + GetState());
        }
        if (elapsed.Elapsed.TotalSeconds - readySince >= ReadySeconds) Finish();
    }

    string GetState()
    {
        var manager = RTFightManager.Target;
        var team1Count = manager != null && manager.team1 != null ? manager.team1.teamMembers.GetValues().Count : 0;
        var team2Count = manager != null && manager.team2 != null ? manager.team2.teamMembers.GetValues().Count : 0;
        var process = FSceneProcessesRunner.Main.currentProcess;
        var title = UILayerLoader.Get<TitleScreenLayer>();
        return $"scene={SceneManager.GetActiveScene().name}; config={Starter.ConfigInitialised}; "
            + $"process={process?.GetType().Name ?? "none"}; team1={team1Count}; team2={team2Count}; "
            + $"title={title != null}; frames={Time.frameCount}; timeScale={Time.timeScale}";
    }

    void Finish()
    {
        finished = true;
        Application.logMessageReceived -= CaptureError;
        var report = new Report
        {
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString(),
            passed = errors.Count == 0,
            elapsedSeconds = elapsed.Elapsed.TotalSeconds,
            state = GetState(),
            errors = errors.ToArray()
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }
        catch (Exception exception)
        {
            errors.Add("Unable to write smoke report: " + exception.Message);
            Debug.LogError(errors[errors.Count - 1]);
        }

        var passed = errors.Count == 0;
        Debug.Log((passed ? "POCKETSTRIKER_PLAYER_SMOKE_PASS " : "POCKETSTRIKER_PLAYER_SMOKE_FAIL ")
            + report.state + "; report=" + reportPath);
        Application.Quit(passed ? 0 : 1);
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= CaptureError;
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using mainMenu;
using UniRx;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary Editor-only capture harness. Records actual Game View rendering;
// never constructs combat, changes teams/skills, awards rewards or edits accounts.
public static partial class PocketStrikerUILiveReview
{
    const int StoreVideoWidth = 886, StoreVideoHeight = 1920, StoreVideoFps = 30;
    static RecorderController storeVideoController;
    static RecorderControllerSettings storeVideoControllerSettings;
    static MovieRecorderSettings storeVideoMovieSettings;
    static IDisposable storeVideoGameOverSubscription;
    static StoreVideoClip storeVideoActiveClip;
    static StoreVideoManifest storeVideoManifest;
    static bool storeVideoStopping;
    static int storeVideoOriginalTargetFrameRate;
    static float storeVideoOriginalCaptureDeltaTime;
    static bool storeVideoOriginalRunInBackground;

    [Serializable] sealed class StoreVideoManifest
    {
        public string scope = "Editor-current production rendering and real navigation. Not a build39 physical-device or Simulator recording.";
        public string sampling = "Unique Time.frameCount observations at end of rendered frames; sample counts are not encoded frame counts. Recorder frame interval controls requested duration.";
        public string encodedValidation = "Pending independent media probe: codec/profile/level, encoded frame rate/count/duration, dimensions, audio and content must be verified from each resulting file.";
        public string unityVersion, language, updatedUtc;
        public bool fixtureAccountUsed, hiddenComboActivated, simulatedFramesUsed;
        public List<StoreVideoClip> clips = new List<StoreVideoClip>();
        public List<StoreVideoOperation> operations = new List<StoreVideoOperation>();
    }

    [Serializable] sealed class StoreVideoClip
    {
        public string label, outputPath, filename, startUtc, endUtc, startScene, endScene;
        public string startProcess, endProcess, stopReason, errorCode, sha256;
        public string actualAudioSpeakerMode;
        public bool editorAudioMuteBeforeCapture, listenerPausedBeforeCapture;
        public float listenerVolumeBeforeCapture, runtimeBgmVolume, runtimeEffectsVolume;
        public int activeAudioListeners, audioOutputProbeFrames, nonzeroAudioProbeFrames;
        public float audioOutputProbePeak;
        public string requestedEncoder = "Unity Media Encoder H.264 MP4; Custom quality; High profile; target 11 Mbps; no alpha; capture native game audio.";
        public bool battle, recorderStarted, requestedIntervalCompleted, fileExists;
        public bool captureTimingRestored, targetFrameRateRestored, runInBackgroundRestored;
        public int requestedWidth = StoreVideoWidth, requestedHeight = StoreVideoHeight;
        public int requestedFps = StoreVideoFps, requestedFrames, sampledUniqueFrames;
        public int startUnityFrame, endUnityFrame, originalCaptureFramerate, originalTargetFrameRate;
        public int actualAudioSampleRate, actualAudioDspBufferSize;
        public int encodedFrameCount = -1;
        public long encodedFileBytes;
        public double requestedSeconds, startRealtime, endRealtime, wallSeconds;
        public double observedSampleRatePerWallSecond;
        public float originalCaptureDeltaTime, startTimeScale, endTimeScale;
        public List<StoreVideoFrame> frames = new List<StoreVideoFrame>();
    }

    [Serializable] sealed class StoreVideoFrame
    {
        public int unityFrame, renderedFrame, width, height, loadedHeroes, loadedEnemies;
        public int activeHeroes, activeEnemies;
        public string utc, scene, process, mode;
        public double realtime, gameTime;
        public float timeScale;
        public bool gameOver, actualFightingProcess, groupBattle;
    }

    [Serializable] sealed class StoreVideoOperation
    {
        public string name, utc, scene;
        public int unityFrame;
        public double realtime;
    }

    [InitializeOnLoadMethod]
    static void StoreVideoRegisterLifecycle()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= StopStoreVideoRecorder;
        AssemblyReloadEvents.beforeAssemblyReload += StopStoreVideoRecorder;
        EditorApplication.quitting -= StopStoreVideoRecorder;
        EditorApplication.quitting += StopStoreVideoRecorder;
        EditorApplication.playModeStateChanged -= StoreVideoOnPlayModeChanged;
        EditorApplication.playModeStateChanged += StoreVideoOnPlayModeChanged;
    }

    static void StoreVideoOnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            StoreVideoStop("exiting-play-mode");
    }

    static string StoreVideoSafeLabel(string value)
    {
        Require(!string.IsNullOrEmpty(value) && value.Length <= 80 &&
            value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c == '-' || c == '_'), "Video label must be a short capture label, without personal data or path characters.");
        return value;
    }

    // The integration may call this around native pointer actions. Pass action
    // labels only, never account names, IDs, server data or exception messages.
    static void StoreVideoRecordOperation(string name)
    {
        StoreVideoEnsureManifest();
        storeVideoManifest.operations.Add(new StoreVideoOperation
        {
            name = StoreVideoSafeLabel(name), utc = DateTime.UtcNow.ToString("O"),
            scene = SceneManager.GetActiveScene().name, unityFrame = Time.frameCount,
            realtime = Time.realtimeSinceStartupAsDouble
        });
        StoreVideoSaveManifest();
    }

    static void StoreVideoNoteAction(string label) => StoreVideoRecordOperation(label);

    static void StoreWriteVideoManifest() => StoreVideoSaveManifest();

    static void StoreVideoEnsureManifest()
    {
        Require(!string.IsNullOrEmpty(StoreOutput), "Store video output directory is missing.");
        Directory.CreateDirectory(StoreOutput);
        storeVideoManifest ??= new StoreVideoManifest { unityVersion = Application.unityVersion, language = StoreLanguage };
    }

    static void StoreVideoSaveManifest()
    {
        if (storeVideoManifest == null || string.IsNullOrEmpty(StoreOutput)) return;
        storeVideoManifest.updatedUtc = DateTime.UtcNow.ToString("O");
        File.WriteAllText(Path.Combine(StoreOutput, "video-manifest.json"), JsonUtility.ToJson(storeVideoManifest, true));
    }

    static string StoreVideoProcess(bool battle)
    {
        return battle ? FSceneProcessesRunner.Main?.currentProcess?.GetType().Name
            : ProcessesRunner.Main?.currentProcess?.GetType().Name;
    }

    static bool StoreVideoCanRecord(bool battle, string expectedScene, out string reason)
    {
        reason = null;
        if (!Application.isPlaying || EditorApplication.isPaused) reason = "play-mode-not-running";
        else if (SceneManager.GetActiveScene().name != expectedScene) reason = "scene-changed";
        else if (Screen.width != StoreVideoWidth || Screen.height != StoreVideoHeight) reason = "render-size-changed";
        else if (Layer<CommonFightResult>() != null) reason = "result-layer-appeared";
        else if (Layer<ProgressLayer>() != null || Layer<PopupLayer>() != null) reason = "readiness-lost";
        else if (battle)
        {
            if (FightLogger.value.GameOver.Value) reason = "battle-game-over";
            else if (FightLoad.Fight == null || RTFightManager.Target == null ||
                FSceneProcessesRunner.Main?.currentProcess is not FightingProcess ||
                Layer<FightingStepLayer>() == null || !Layer<FightingStepLayer>().PauseButton.IsInteractable())
                reason = "battle-readiness-lost";
        }
        else if (ProcessesRunner.Main == null || !Ready) reason = "page-readiness-lost";
        return reason == null;
    }

    static StoreVideoFrame StoreVideoObserveFrame(bool battle)
    {
        var frame = new StoreVideoFrame
        {
            unityFrame = Time.frameCount, renderedFrame = Time.renderedFrameCount,
            utc = DateTime.UtcNow.ToString("O"), realtime = Time.realtimeSinceStartupAsDouble,
            gameTime = Time.timeAsDouble, timeScale = Time.timeScale,
            scene = SceneManager.GetActiveScene().name, process = StoreVideoProcess(battle),
            width = Screen.width, height = Screen.height
        };
        if (battle)
        {
            var manager = RTFightManager.Target;
            frame.mode = FightLoad.Fight.FightMode.ToString();
            frame.groupBattle = FightLoad.Fight.IsGroupBattle;
            frame.gameOver = FightLogger.value.GameOver.Value;
            frame.actualFightingProcess = FSceneProcessesRunner.Main?.currentProcess is FightingProcess;
            frame.loadedHeroes = manager.team1.teamMembers.GetValues().Count;
            frame.loadedEnemies = manager.team2.teamMembers.GetValues().Count;
            frame.activeHeroes = manager.team1.teamMembers.GetValues().Count(unit => unit != null && unit.WholeT != null && unit.WholeT.gameObject.activeInHierarchy && !unit.FightDataRef.IsDead.Value);
            frame.activeEnemies = manager.team2.teamMembers.GetValues().Count(unit => unit != null && unit.WholeT != null && unit.WholeT.gameObject.activeInHierarchy && !unit.FightDataRef.IsDead.Value);
        }
        return frame;
    }

    static void StoreVideoWatchSafety()
    {
        if (storeVideoActiveClip == null || storeVideoController == null || storeVideoStopping) return;
        try
        {
            if (!StoreVideoCanRecord(storeVideoActiveClip.battle, storeVideoActiveClip.startScene, out var reason))
                StoreVideoStop(reason);
        }
        catch (Exception)
        {
            StoreVideoStop("runtime-readiness-unavailable");
        }
    }

    // Call before global Finish/pause/settings/scene restoration as well as from
    // lifecycle callbacks. Safe even after PrepareRecording or a failed start.
    static void StopStoreVideoRecorder() => StoreVideoStop("external-stop-or-domain-reload");

    static void StoreVideoStop(string reason)
    {
        if (storeVideoStopping || (storeVideoController == null && storeVideoActiveClip == null)) return;
        storeVideoStopping = true;
        EditorApplication.update -= StoreVideoWatchSafety;
        var clip = storeVideoActiveClip;
        try
        {
            storeVideoGameOverSubscription?.Dispose();
            storeVideoGameOverSubscription = null;
            if (clip != null)
            {
                clip.stopReason ??= reason;
                clip.endUtc = DateTime.UtcNow.ToString("O");
                clip.endRealtime = Time.realtimeSinceStartupAsDouble;
                clip.endUnityFrame = Time.frameCount;
                clip.endScene = SceneManager.GetActiveScene().name;
                clip.endProcess = StoreVideoProcess(clip.battle);
                clip.endTimeScale = Time.timeScale;
            }
            // Stop/finalize and drain GPU readback before restoring any timing.
            storeVideoController?.StopRecording();
        }
        catch (Exception exception)
        {
            if (clip != null) clip.errorCode = "stop-" + exception.GetType().Name;
        }
        finally
        {
            storeVideoController = null;
            Time.captureDeltaTime = storeVideoOriginalCaptureDeltaTime;
            Application.targetFrameRate = storeVideoOriginalTargetFrameRate;
            Application.runInBackground = storeVideoOriginalRunInBackground;
            if (clip != null)
            {
                clip.captureTimingRestored = Mathf.Approximately(Time.captureDeltaTime, clip.originalCaptureDeltaTime) && Time.captureFramerate == clip.originalCaptureFramerate;
                clip.targetFrameRateRestored = Application.targetFrameRate == clip.originalTargetFrameRate;
                clip.runInBackgroundRestored = Application.runInBackground == storeVideoOriginalRunInBackground;
                clip.sampledUniqueFrames = clip.frames.Count;
                clip.wallSeconds = Math.Max(0, clip.endRealtime - clip.startRealtime);
                if (clip.frames.Count > 1)
                {
                    double elapsed = clip.frames[clip.frames.Count - 1].realtime - clip.frames[0].realtime;
                    clip.observedSampleRatePerWallSecond = elapsed > 0 ? (clip.frames.Count - 1) / elapsed : 0;
                }
                clip.fileExists = File.Exists(clip.outputPath);
                if (clip.fileExists)
                {
                    clip.encodedFileBytes = new FileInfo(clip.outputPath).Length;
                    if (clip.encodedFileBytes > 0) clip.sha256 = StoreSha(File.ReadAllBytes(clip.outputPath));
                }
            }
            if (storeVideoMovieSettings != null) UnityEngine.Object.DestroyImmediate(storeVideoMovieSettings);
            if (storeVideoControllerSettings != null) UnityEngine.Object.DestroyImmediate(storeVideoControllerSettings);
            storeVideoMovieSettings = null;
            storeVideoControllerSettings = null;
            storeVideoActiveClip = null;
            storeVideoStopping = false;
            StoreVideoSaveManifest();
        }
    }

    static async UniTask StoreVideoRecordClip(string label, double seconds, bool battle)
    {
        Require(storeVideoController == null && storeVideoActiveClip == null, "Another video clip is active.");
        Require(seconds > 0 && seconds <= 30 && !double.IsNaN(seconds), "Video clip duration must be between zero and 30 seconds.");
        StoreVideoEnsureManifest();
        label = StoreVideoSafeLabel(label);
        string scene = SceneManager.GetActiveScene().name;
        Require(StoreVideoCanRecord(battle, scene, out var initialReason), "Cannot record current real Game View: " + initialReason);
        string stem = label + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var clip = new StoreVideoClip
        {
            label = label, battle = battle, filename = stem + ".mp4",
            outputPath = Path.GetFullPath(Path.Combine(StoreOutput, stem + ".mp4")),
            requestedSeconds = seconds, requestedFrames = (int)Math.Ceiling(seconds * StoreVideoFps),
            startUtc = DateTime.UtcNow.ToString("O"), startRealtime = Time.realtimeSinceStartupAsDouble,
            startUnityFrame = Time.frameCount, startScene = scene, startProcess = StoreVideoProcess(battle),
            startTimeScale = Time.timeScale, originalCaptureFramerate = Time.captureFramerate,
            originalCaptureDeltaTime = Time.captureDeltaTime, originalTargetFrameRate = Application.targetFrameRate
        };
        clip.editorAudioMuteBeforeCapture = EditorUtility.audioMasterMute;
        clip.listenerPausedBeforeCapture = AudioListener.pause;
        clip.listenerVolumeBeforeCapture = AudioListener.volume;
        clip.runtimeBgmVolume = AppSetting.Value.BgmVolume;
        clip.runtimeEffectsVolume = AppSetting.Value.EffectsVolume;
        clip.activeAudioListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Count(x => x.enabled);
        Require(!EditorUtility.audioMasterMute && !AudioListener.pause && AudioListener.volume > 0 && clip.activeAudioListeners == 1, "Actual capture audio is muted or lacks exactly one listener.");
        var audioConfiguration = AudioSettings.GetConfiguration();
        clip.actualAudioSpeakerMode = audioConfiguration.speakerMode.ToString();
        clip.actualAudioSampleRate = audioConfiguration.sampleRate;
        clip.actualAudioDspBufferSize = audioConfiguration.dspBufferSize;
        Require(!File.Exists(clip.outputPath), "Video output already exists; refusing overwrite.");
        storeVideoOriginalCaptureDeltaTime = Time.captureDeltaTime;
        storeVideoOriginalTargetFrameRate = Application.targetFrameRate;
        storeVideoOriginalRunInBackground = Application.runInBackground;
        storeVideoActiveClip = clip;
        storeVideoManifest.clips.Add(clip);
        StoreVideoSaveManifest();
        try
        {
            storeVideoControllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            storeVideoControllerSettings.hideFlags = HideFlags.HideAndDontSave;
            storeVideoControllerSettings.FrameRate = StoreVideoFps;
            storeVideoControllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            storeVideoControllerSettings.CapFrameRate = true;
            storeVideoControllerSettings.ExitPlayMode = false;
            // Inclusive interval: 0..N-1 requests N actual Recorder frames.
            storeVideoControllerSettings.SetRecordModeToFrameInterval(0, clip.requestedFrames - 1);
            storeVideoMovieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            storeVideoMovieSettings.hideFlags = HideFlags.HideAndDontSave;
            storeVideoMovieSettings.name = "PocketStrikerRealGameViewVideo";
            storeVideoMovieSettings.Enabled = true;
            storeVideoMovieSettings.OutputFile = Path.Combine(StoreOutput, stem);
            storeVideoMovieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = StoreVideoWidth, OutputHeight = StoreVideoHeight };
            storeVideoMovieSettings.CaptureAlpha = false;
            storeVideoMovieSettings.CaptureAudio = true;
            storeVideoMovieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High,
                TargetBitRate = 11f,
                GopSize = StoreVideoFps,
                NumConsecutiveBFrames = 2
            };
            storeVideoControllerSettings.AddRecorderSettings(storeVideoMovieSettings);
            storeVideoController = new RecorderController(storeVideoControllerSettings);
            Application.targetFrameRate = StoreVideoFps;
            EditorApplication.update += StoreVideoWatchSafety;
            if (battle)
                storeVideoGameOverSubscription = FightLogger.value.GameOver.Subscribe(over =>
                {
                    // Synchronous stop on the real outcome signal; no result,
                    // award or synthetic game-state action is dispatched here.
                    if (over) StoreVideoStop("battle-game-over");
                });
            storeVideoController.PrepareRecording();
            Require(storeVideoController.StartRecording(), "Recorder could not start the actual Game View movie.");
            clip.recorderStarted = true;
            StoreVideoSaveManifest();
            double deadline = Time.realtimeSinceStartupAsDouble + Math.Max(60, seconds * 6);
            int lastSampledFrame = -1;
            while (storeVideoController != null && storeVideoController.IsRecording())
            {
                await UniTask.WaitForEndOfFrame(cancellation?.Token ?? default);
                if (storeVideoController == null) break;
                if (!StoreVideoCanRecord(battle, scene, out var reason))
                {
                    StoreVideoStop(reason);
                    break;
                }
                if (Time.frameCount != lastSampledFrame)
                {
                    clip.frames.Add(StoreVideoObserveFrame(battle));
                    var pcmProbe = new float[1024];
                    AudioListener.GetOutputData(pcmProbe, 0);
                    float probePeak = pcmProbe.Max(x => Mathf.Abs(x));
                    clip.audioOutputProbeFrames++;
                    if (probePeak > .000001f) clip.nonzeroAudioProbeFrames++;
                    clip.audioOutputProbePeak = Mathf.Max(clip.audioOutputProbePeak, probePeak);
                    lastSampledFrame = Time.frameCount;
                }
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    throw new TimeoutException("Real Game View recorder did not finish its requested frame interval.");
            }
            if (storeVideoController != null)
            {
                clip.requestedIntervalCompleted = true;
                StoreVideoStop("requested-frame-interval-complete");
            }
        }
        catch (Exception exception)
        {
            clip.errorCode ??= "capture-" + exception.GetType().Name;
            throw;
        }
        finally
        {
            // Integration must only pause/return/restore after this completes.
            StoreVideoStop(clip.errorCode == null ? "capture-finally" : "capture-error");
            StoreVideoSaveManifest();
        }
        Require(clip.recorderStarted && clip.sampledUniqueFrames > 0 && clip.fileExists && clip.encodedFileBytes > 0,
            "Recorder produced no nonempty video with observed real rendered frames.");
        Require((clip.requestedIntervalCompleted || clip.stopReason == "battle-game-over") && clip.errorCode == null,
            "Video stopped before its requested frame interval: " + clip.stopReason + ". Stop navigation before any result/reward action.");
    }
}
#endif

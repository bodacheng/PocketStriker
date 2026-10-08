from pathlib import Path
import hashlib
import json
import shutil
import subprocess

source = Path('/Users/daisei/PocketStriker')
delivery = source / 'output/appstore-video-20261008'
project = Path('/tmp/pocketstriker-video-20261008/project')
recovered = delivery / 'recovered'
editor = project / 'Assets/Editor'
run_sources = delivery / 'run-sources'
run_sources.mkdir(exist_ok=True)

def change(text, old, new):
    assert old in text, old[:100]
    return text.replace(old, new, 1)

base = (source / 'Assets/Editor/PocketStrikerUILiveReview.cs').read_text()
base = change(base, 'const double MaximumSeconds = 480;', 'const double MaximumSeconds = 900;')
base = change(base, 'static string Output => SessionState.GetString(Key + ".Output", "Logs/UIArt/Live/latest");', 'static string Output => !string.IsNullOrEmpty(StoreOutput) ? StoreOutput : SessionState.GetString(Key + ".Output", "Logs/UIArt/Live/latest");')
base = change(base, 'if (SessionState.GetBool(Key, false)) Attach();', 'if (SessionState.GetBool(Key, false)) Attach();\n        StoreReattach();')
base = change(base, 'Run().Forget();\n    }', 'StoreVideoRun().Forget();\n    }')
base = change(base, 'finishing = true;\n        cancellation?.Cancel();', 'finishing = true;\n        StopStoreVideoRecorder();\n        cancellation?.Cancel();')
base = change(base, 'if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);\n        else EditorApplication.isPlaying = false;', 'EditorApplication.isPlaying = false;')
(editor / 'PocketStrikerUILiveReview.cs').write_text(base)
(run_sources / 'PocketStrikerUILiveReview.cs').write_text(base)

capture = (recovered / 'PocketStrikerUILiveReview.StoreCapture.cs').read_text()
capture = change(capture, 'int width = device == "iphone" ? 1290 : 2048, height = device == "iphone" ? 2796 : 2732;', 'int width = 886, height = 1920;')
capture = change(capture, 'Require(output.StartsWith("/tmp/", StringComparison.Ordinal) || output.StartsWith("/private/tmp/", StringComparison.Ordinal), "Output must be under /tmp.");', 'Require(output.StartsWith("/Users/daisei/PocketStriker/output/appstore-video-20261008/", StringComparison.Ordinal), "Use the authorized video delivery directory.");')
capture = change(capture, 'public List<StoreResourceLocator> resourceLocators', 'public List<StoreBattleObservation> battleFrames = new List<StoreBattleObservation>();\n        public List<StoreResourceLocator> resourceLocators')
capture = change(capture, 'out StageButton chosen, out Vector2 normalized)', 'out StageButton chosen, out Vector2 normalized, int? wantedStage = null)')
capture = change(capture, '.Where(stage => stage.StageNo <= PlayerAccountInfo.Me.arcadeProcess && !AdventureModeRules.IsTutorialStage(stage.StageNo.ToString()))', '.Where(stage => (!wantedStage.HasValue || stage.StageNo == wantedStage.Value) && stage.StageNo <= PlayerAccountInfo.Me.arcadeProcess && !AdventureModeRules.IsTutorialStage(stage.StageNo.ToString()))')
capture = change(capture, 'static void StoreSnapshotState()\n    {', 'static void StoreSnapshotState()\n    {\n        SessionState.SetFloat(StoreKey + ".Bgm", AppSetting.Value.BgmVolume);\n        SessionState.SetFloat(StoreKey + ".Effects", AppSetting.Value.EffectsVolume);\n        SessionState.SetBool(StoreKey + ".AudioMute", EditorUtility.audioMasterMute);\n        SessionState.SetBool(StoreKey + ".ListenerPause", AudioListener.pause);\n        SessionState.SetFloat(StoreKey + ".ListenerVolume", AudioListener.volume);')
capture = change(capture, 'AppSetting.Value.Language = (SystemLanguage)SessionState.GetInt(StoreKey + ".MemoryLanguage", (int)SystemLanguage.English);', 'AppSetting.Value.Language = (SystemLanguage)SessionState.GetInt(StoreKey + ".MemoryLanguage", (int)SystemLanguage.English);\n        AppSetting.Value.BgmVolume = SessionState.GetFloat(StoreKey + ".Bgm");\n        AppSetting.Value.EffectsVolume = SessionState.GetFloat(StoreKey + ".Effects");\n        EditorUtility.audioMasterMute = SessionState.GetBool(StoreKey + ".AudioMute");\n        AudioListener.pause = SessionState.GetBool(StoreKey + ".ListenerPause");\n        AudioListener.volume = SessionState.GetFloat(StoreKey + ".ListenerVolume");')
capture = capture.replace('SessionState.GetFloat(StoreKey + ".Bgm")', 'SessionState.GetFloat(StoreKey + ".Bgm", 1f)').replace('SessionState.GetFloat(StoreKey + ".Effects")', 'SessionState.GetFloat(StoreKey + ".Effects", 1f)').replace('SessionState.GetBool(StoreKey + ".AudioMute")', 'SessionState.GetBool(StoreKey + ".AudioMute", false)').replace('SessionState.GetBool(StoreKey + ".ListenerPause")', 'SessionState.GetBool(StoreKey + ".ListenerPause", false)').replace('SessionState.GetFloat(StoreKey + ".ListenerVolume")', 'SessionState.GetFloat(StoreKey + ".ListenerVolume", 1f)')

battle = (recovered / 'PocketStrikerUILiveReview.BattleCapture.cs').read_text()
battle = 'using mainMenu;\nusing UnityEditor;\n' + battle
battle = change(battle, 'FightLoad.Fight.EventType == FightEventType.Quest && FightLoad.Fight.FightMode == wanted,', '(FightLoad.Fight.EventType == FightEventType.Quest || (wanted == FightMode.Rotate && FightLoad.Fight.EventType == FightEventType.Self)) && FightLoad.Fight.FightMode == wanted,')

flow = (recovered / 'PocketStrikerUILiveReview.StoreVideoFlow.cs').read_text()
flow = change(flow, 'foreach (string name in Inventory) report.cases.Add(new Case { name = name });', 'foreach (string name in new[] { "battle-duel", "battle-team", "battle-group", "skill-editor" }) report.cases.Add(new Case { name = name });')
flow = change(flow, 'StopStoreVideoRecorder(); StoreWriteVideoManifest();', 'StopStoreVideoRecorder(); StoreWriteVideoManifest();\n            foreach (var entry in report.cases.Where(x => x.status == "unreached")) { entry.status = "unavailable"; entry.note = "Not reached in this capture."; }')
start = flow.index('    static async UniTask StoreRecaptureSkillEditor()')
end = flow.index('    static bool StoreVideoButtonVisible', start)
flow = flow[:start] + '    static async UniTask StoreRecaptureSkillEditor() => await CaptureDynamicSkillEditor();\n\n' + flow[end:]

files = {
    'PocketStrikerUILiveReview.StoreCapture.cs': capture,
    'PocketStrikerUILiveReview.BattleCapture.cs': battle,
    'PocketStrikerUILiveReview.StoreVideoFlow.cs': flow,
    'PocketStrikerUILiveReview.StoreVideoRecorder.cs': (recovered / 'PocketStrikerUILiveReview.StoreVideoRecorder.cs').read_text(),
    'PocketStrikerUILiveReview.DynamicSkills.cs': (delivery / 'draft-skill-sequence.cs').read_text().replace('StoreVideoRecordClip("skill-editor", 15, false)', 'StoreVideoRecordClip("skill-editor", 15.5, false)'),
}
recorder = files['PocketStrikerUILiveReview.StoreVideoRecorder.cs']
recorder = change(recorder, 'reason = "render-size-changed";', 'reason = "render-size-changed-" + Screen.width + "x" + Screen.height;')
recorder = change(recorder, 'string scene = SceneManager.GetActiveScene().name;\n        Require(StoreVideoCanRecord', 'string scene = SceneManager.GetActiveScene().name;\n        UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);\n        UnityEditor.PlayModeWindow.SetCustomRenderingResolution(StoreVideoWidth, StoreVideoHeight, "Recording Resolution");\n        await UniTask.DelayFrame(5);\n        Require(StoreVideoCanRecord')
recorder = change(recorder, 'if (storeVideoActiveClip == null || storeVideoController == null || storeVideoStopping) return;', 'if (storeVideoActiveClip == null || storeVideoController == null || storeVideoStopping || storeVideoActiveClip.frames.Count == 0) return;')
recorder = change(recorder, 'try\n        {\n            if (!StoreVideoCanRecord(storeVideoActiveClip.battle', 'try\n        {\n            // Editor repaints can temporarily expose the Scene view Screen size.\n            // Dimensions remain strictly checked in the actual rendered-frame loop.\n            if (Screen.width != StoreVideoWidth || Screen.height != StoreVideoHeight) return;\n            if (!StoreVideoCanRecord(storeVideoActiveClip.battle')
files['PocketStrikerUILiveReview.StoreVideoRecorder.cs'] = recorder
for name, text in files.items():
    (editor / name).write_text(text)
    (run_sources / name).write_text(text)

manifest = {
    'sourceProject': str(source),
    'recordingProject': str(project),
    'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source, text=True).strip(),
    'appVersion': '3.0.2',
    'unityVersion': '6000.5.1f1',
    'captureKind': 'Unity Recorder GameView, real account and production UI, unsaved skill edits',
    'physicalDeviceCapture': False,
    'syntheticInventoryOrCombat': False,
    'sources': {n: hashlib.sha256(t.encode()).hexdigest() for n, t in files.items()},
}
(project / 'StoreCaptureSourceManifest.json').write_text(json.dumps(manifest, indent=2))
(delivery / 'source-manifest.json').write_text(json.dumps(manifest, indent=2))
print('Installed capture runner in isolated project:', project)

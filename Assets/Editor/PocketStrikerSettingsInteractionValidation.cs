using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real settings prefab callbacks with isolated account and resource service responses.</summary>
public static class PocketStrikerSettingsInteractionValidation
{
    const string PrefabPath = "Assets/Resources/DummyLayerSystem/SettingLayer.prefab";
    const string ReportPath = "Logs/UILayout/settings-interactions.json";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool running;

    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public string unityVersion;
        public string utcTime;
        public int tabTransitions;
        public int languageLoads;
        public int languageRefreshes;
        public int nicknameOpens;
        public int nicknameNotifications;
        public int emailConfirmations;
        public int passwordResetRequests;
        public bool sourcePrefabUnchanged;
        public bool currentScenesUnchanged;
        public string scope = "Instantiated production SettingLayer and actual Button.onClick callbacks. Twelve initialization/tab cycles, language service completion/failure and reentrant clicks, nickname success/cancel/stale callbacks, repeated account states and preserved unrelated listeners. Instance service seams count effects without network calls.";
        public string limitation = "Isolated Editor component interaction, not full Play-mode navigation, screen raycasts, account services or physical-device touch timing. The failure fixture invokes the same async handler directly so its expected exception is observed without an unhandled log.";
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Settings Interactions")]
    public static async void Validate() { await Run(false); }

    // Run without -quit. The asynchronous entry exits after all checks complete.
    public static async void ValidateBatch() { await Run(true); }

    static async UniTask Run(bool exitWhenDone)
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Settings interaction validation requires a stopped editor.");
        running = true;
        var report = new Report { unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        var sourceText = File.ReadAllText(PrefabPath);
        var oldAccount = PlayerAccountInfo.Me;
        var oldSettings = AppSetting.Value;
        var oldConverters = LanguageConverterManger.List;
        var oldCanvas = PosCal.Canvas;
        var oldSafe = PosCal.SafeAreaRect;
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var dirty = scenes.Select(scene => scene.isDirty).ToArray();
        var activeScene = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            PlayerAccountInfo.Me = new PlayerAccountInfo
            {
                PlayFabId = "LOCAL_SETTINGS_FIXTURE", TitleDisplayName = "Before", PlayFabUserName = "local",
                Email = "fixture@example.invalid", tutorialProgress = "Finished"
            };
            AppSetting.Value = new AppSetting { Language = SystemLanguage.English };
            LanguageConverterManger.List = new List<LanguageConverter>();
            var rig = new GameObject("Settings Interaction Fixture", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(rig, preview);
            var canvas = rig.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)rig.transform;
            canvasRect.sizeDelta = new Vector2(1080, 1920);
            PosCal.Canvas = canvas;
            PosCal.SafeAreaRect = canvasRect;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(source != null, "Settings prefab is missing.");
            var layer = UnityEngine.Object.Instantiate(source, rig.transform, false).GetComponent<SettingLayer>();
            Require(layer != null, "SettingLayer is missing.");
            var root = (RectTransform)layer.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            foreach (var animator in layer.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var group in layer.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            layer.ResizeAreas();

            Set(layer, "_changeLanguagePresentation", (Action)(() => { }));
            Set(layer, "_reloadSkillNames", (Func<UniTask>)(() => { report.languageLoads++; return UniTask.CompletedTask; }));
            Set(layer, "_refreshSkillConfig", (Action)(() => report.languageRefreshes++));
            Set(layer, "_nicknameSaved", (Action)(() => report.nicknameNotifications++));
            Set(layer, "_confirmEmailForValidation", (Action)(() => report.emailConfirmations++));
            Set(layer, "_sendPasswordResetForValidation", (Action)(() => report.passwordResetRequests++));
            Action<string> nicknameSuccess = null;
            Action nicknameCancel = null;
            Set(layer, "_openNickname", (Action<Action<string>, Action>)((success, cancel) =>
            {
                report.nicknameOpens++;
                nicknameSuccess = success;
                nicknameCancel = cancel;
            }));
            var en = Get<BOButton>(layer, "enBtn");
            var jp = Get<BOButton>(layer, "jpBtn");
            var ch = Get<BOButton>(layer, "chBtn");
            var reset = Get<BOButton>(layer, "resetNickNameBtn");
            var confirmEmail = Get<BOButton>(layer, "EmailConfirmBtn");
            var passwordReset = Get<BOButton>(layer, "SendPwResetBtn");
            int unrelatedLanguage = 0, unrelatedNickname = 0, unrelatedEmail = 0, unrelatedReset = 0;
            en.onClick.AddListener(() => unrelatedLanguage++);
            reset.onClick.AddListener(() => unrelatedNickname++);
            confirmEmail.onClick.AddListener(() => unrelatedEmail++);
            passwordReset.onClick.AddListener(() => unrelatedReset++);

            var tabs = new[] { "volume", "account", "device", "support", "language", "nickName" };
            for (int cycle = 0; cycle < 12; cycle++)
            {
                layer.Initialise();
                foreach (string tab in tabs)
                {
                    Get<BOButton>(layer, tab + "Btn").onClick.Invoke();
                    Require(tabs.All(other => Get<RectTransform>(layer, other + "Panel").gameObject.activeSelf == (other == tab)),
                        "Tab visibility changed after repeated initialization: " + tab);
                    report.tabTransitions++;
                }
            }
            Check(report, "72 tab transitions retained exactly one visible panel.");
            PlayerAccountInfo.Me.TitleDisplayName = "Refreshed";
            PlayerAccountInfo.Me.PlayFabUserName = "new-local";
            AppSetting.Value.BgmVolume = 0.2f;
            AppSetting.Value.EffectsVolume = 0.7f;
            layer.Initialise();
            Require(Get<Text>(layer, "nickName").text == "Refreshed" && Get<InputField>(layer, "CurrentEmail").text == "new-local"
                && Mathf.Approximately(Get<Slider>(layer, "bgmSlider").value, 0.2f)
                && Mathf.Approximately(Get<Slider>(layer, "effectsSoundsSlider").value, 0.7f),
                "The binding guard prevented current account/audio values from refreshing.");
            Check(report, "Repeated initialization refreshes account labels and audio values.");

            Get<BOButton>(layer, "languageBtn").onClick.Invoke();
            foreach (var pair in new[] { (jp, SystemLanguage.Japanese), (ch, SystemLanguage.Chinese), (en, SystemLanguage.English) })
            {
                pair.Item1.onClick.Invoke();
                Require(AppSetting.Value.Language == pair.Item2 && Get<GameObject>(layer, "selectedIndicator").transform.parent == pair.Item1.transform,
                    "Language click did not update the selected indicator.");
                pair.Item1.onClick.Invoke();
            }
            Require(report.languageLoads == 3 && report.languageRefreshes == 3, "A language click started duplicate resource loads.");
            Require(unrelatedLanguage == 2, "Initialization discarded an unrelated language listener.");
            Check(report, "Each changed language loads/refreshes once; selecting the same language performs no redundant load.");

            var pending = new UniTaskCompletionSource();
            Set(layer, "_reloadSkillNames", (Func<UniTask>)(() => { report.languageLoads++; return pending.Task; }));
            jp.onClick.Invoke();
            for (int i = 0; i < 8; i++) { jp.onClick.Invoke(); ch.onClick.Invoke(); }
            Require(report.languageLoads == 4 && report.languageRefreshes == 3 && AppSetting.Value.Language == SystemLanguage.Japanese
                && !en.interactable && !jp.interactable && !ch.interactable, "Pending language load admitted a duplicate or changed language.");
            pending.TrySetResult();
            await UniTask.Yield();
            Require(report.languageRefreshes == 4 && en.interactable && jp.interactable && ch.interactable,
                "Completed language load did not refresh once and release its controls.");
            Check(report, "Sixteen reentrant clicks during an incomplete load produce one request, then controls recover.");

            Set(layer, "_reloadSkillNames", (Func<UniTask>)(() => UniTask.FromException(new InvalidOperationException("EXPECTED_SETTINGS_FAILURE"))));
            bool observedFailure = false;
            try { await (UniTask)typeof(SettingLayer).GetMethod("ChangeLanguage", Private).Invoke(layer, new object[] { SystemLanguage.Chinese }); }
            catch (InvalidOperationException exception) { observedFailure = exception.Message == "EXPECTED_SETTINGS_FAILURE"; }
            Require(observedFailure && en.interactable && jp.interactable && ch.interactable
                && AppSetting.Value.Language == SystemLanguage.Japanese,
                "A failed language load left controls blocked or a half-selected language.");
            Set(layer, "_reloadSkillNames", (Func<UniTask>)(() => { report.languageLoads++; return UniTask.CompletedTask; }));
            ch.onClick.Invoke();
            Require(report.languageLoads == 5 && report.languageRefreshes == 5
                && AppSetting.Value.Language == SystemLanguage.Chinese,
                "The same language could not be retried after a resource failure.");
            Check(report, "Language-load failure releases controls and a later selection succeeds.");

            Get<BOButton>(layer, "nickNameBtn").onClick.Invoke();
            reset.onClick.Invoke();
            var staleSuccess = nicknameSuccess;
            var staleCancel = nicknameCancel;
            for (int i = 0; i < 8; i++) reset.onClick.Invoke();
            Require(report.nicknameOpens == 1 && !layer.gameObject.activeSelf, "Repeated nickname clicks opened multiple dialogs.");
            nicknameCancel();
            Require(layer.gameObject.activeSelf, "Nickname cancellation failed to reopen settings.");
            reset.onClick.Invoke();
            staleCancel();
            staleSuccess("Stale");
            Require(report.nicknameOpens == 2 && !layer.gameObject.activeSelf && Get<Text>(layer, "nickName").text != "Stale",
                "A stale nickname callback reopened or altered a newer dialog.");
            nicknameSuccess("Saved Name");
            nicknameSuccess("Duplicate");
            Require(layer.gameObject.activeSelf && Get<Text>(layer, "nickName").text == "Saved Name"
                && report.nicknameNotifications == 1 && unrelatedNickname == 10,
                "Nickname completion was duplicated or an unrelated listener was discarded.");
            Check(report, "Nickname opens once; cancel/reopen and stale/duplicate callbacks preserve the current dialog.");

            for (int i = 0; i < 12; i++) { layer.AccountPhase_EmailToBeSet(); layer.AccountPhase_EmailSet(); }
            layer.AccountPhase_EmailToBeSet();
            confirmEmail.onClick.Invoke();
            layer.AccountPhase_EmailSet();
            passwordReset.onClick.Invoke();
            Require(report.emailConfirmations == 1 && report.passwordResetRequests == 1 && unrelatedEmail == 1 && unrelatedReset == 1,
                "Repeated account phases duplicated an account request or discarded an unrelated listener.");
            Check(report, "Repeated email states send one injected request per click and preserve unrelated listeners.");
        }
        catch (Exception exception) { report.errors.Add(exception.ToString()); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            PlayerAccountInfo.Me = oldAccount;
            AppSetting.Value = oldSettings;
            LanguageConverterManger.List = oldConverters;
            PosCal.Canvas = oldCanvas;
            PosCal.SafeAreaRect = oldSafe;
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            report.sourcePrefabUnchanged = File.ReadAllText(PrefabPath) == sourceText;
            report.currentScenesUnchanged = SceneManager.sceneCount == scenes.Length
                && scenes.Select((scene, index) => scene.IsValid() && scene.isDirty == dirty[index]).All(value => value);
            if (!report.sourcePrefabUnchanged || !report.currentScenesUnchanged) report.errors.Add("Validation changed source prefab or open scenes.");
            report.passed = report.errors.Count == 0 && report.checks.Count == 7;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            running = false;
        }
        if (report.passed) Debug.Log("[Settings Interactions] PASS: " + report.checks.Count + " checks, " + report.tabTransitions + " tab transitions. " + ReportPath);
        else Debug.LogError("[Settings Interactions] FAIL: " + string.Join("\n", report.errors));
        if (exitWhenDone) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    static T Get<T>(SettingLayer layer, string name) => (T)typeof(SettingLayer).GetField(name, Private).GetValue(layer);
    static void Set(SettingLayer layer, string name, object value) => typeof(SettingLayer).GetField(name, Private).SetValue(layer, value);
    static void Check(Report report, string message) => report.checks.Add(message);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

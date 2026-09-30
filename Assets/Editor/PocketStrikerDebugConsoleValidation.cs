using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IngameDebugConsole;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Checks real console rows, long stack traces, clipboard and portrait layout.</summary>
[InitializeOnLoad]
public static class PocketStrikerDebugConsoleValidation
{
    const string ConsolePath = "Assets/Diagnostics/PocketStrikerDebugConsole.prefab";
    const string RowPath = "Assets/Diagnostics/PocketStrikerDebugLogItem.prefab";
    const string FontPath = "Assets/Diagnostics/PocketStrikerDebugConsoleFont.asset";
    const string StateKey = "PocketStriker.DebugConsoleValidation";
    const string Output = "Logs/DebugConsoleUpgrade";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static DebugLogManager manager;
    static int startFrame;
    static int step;
    static string trace;
    static readonly List<string> errors = new List<string>();

    [Serializable]
    class Report
    {
        public bool passed;
        public string unityVersion;
        public string packageVersion;
        public int fullLogLength;
        public int screenWidth;
        public int screenHeight;
        public string[] errors;
    }

    static PocketStrikerDebugConsoleValidation()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(StateKey + ".Done", false))
            {
                SessionState.SetBool(StateKey + ".Done", false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(StateKey + ".Exit", 1));
                else
                {
                    var scene = SessionState.GetString(StateKey + ".Scene", "");
                    if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
                }
            }
        };
    }

    [MenuItem("PocketStriker/Validation/Prepare Debug Console Assets")]
    public static void PrepareAssets()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (asset == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
            Require(source != null, "Noto font is missing.");
            asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "PocketStriker Debug Console Noto";
            asset.material.name = asset.name + " Material";
            AssetDatabase.CreateAsset(asset, FontPath);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures)
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
        }
        foreach (var path in new[] { ConsolePath, RowPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = asset;
                    text.fontSharedMaterial = asset.material;
                }
                var console = root.GetComponent<DebugLogManager>();
                if (console != null)
                {
                    var serialized = new SerializedObject(console);
                    serialized.FindProperty("logItemFontOverride").objectReferenceValue = asset;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var layout = new SerializedObject(root.GetComponent<PocketStrikerDebugConsoleLayout>());
                    layout.FindProperty("logPopup").objectReferenceValue = root.GetComponentInChildren<DebugLogPopup>(true).transform;
                    layout.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Debug console fonts and project prefabs prepared.");
    }

    // Run without -quit; this method exits batch mode after the Play Mode checks.
    [MenuItem("PocketStriker/Validation/Debug Console")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        errors.Clear();
        Directory.CreateDirectory(Output);
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save open scenes before validation.");
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DebugLogManager).Assembly);
        Require(package != null && package.version == "1.9.0", "Expected UPM console v1.9.0.");
        ValidateAssets();
        ValidateSafeAreas();
        SessionState.SetString(StateKey + ".Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(PocketStrikerValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        SessionState.SetBool(StateKey, true);
        SessionState.SetString(StateKey + ".Start", DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    static void ValidateAssets()
    {
        foreach (var path in new[] { ConsolePath, RowPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(root != null, "Missing prefab: " + path);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing console script.");
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                Require(text.font != null && text.font.sourceFontFile != null, "Missing CJK font: " + text.name);
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scene/ABLoadScene/Scene1.unity");
            var starter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Starter>(true)).Single();
            var console = new SerializedObject(starter).FindProperty("inGameDebugConsole").objectReferenceValue as DebugLogManager;
            Require(console != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(console) == ConsolePath,
                "Starter is not linked to the new project console prefab.");
        }
        finally
        {
            if (!Application.isBatchMode && setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    static void ValidateSafeAreas()
    {
        foreach (var screen in new[] { new Vector2(540, 960), new Vector2(1206, 2622), new Vector2(1536, 2048) })
        foreach (var fraction in new[] { 0f, 0.25f, 0.6f })
        {
            var safe = new Rect(12, 34, screen.x - 24, screen.y - 112);
            var min = new Vector2(0, fraction);
            var max = Vector2.one;
            var actualMin = Vector2.Scale(min, screen) + PocketStrikerDebugConsoleLayout.SafeAreaOffset(safe, screen.x, screen.y, min);
            var actualMax = Vector2.Scale(max, screen) + PocketStrikerDebugConsoleLayout.SafeAreaOffset(safe, screen.x, screen.y, max);
            Require(Mathf.Abs(actualMin.y - (safe.yMin + safe.height * fraction)) < 0.01f
                && Mathf.Abs(actualMin.x - safe.xMin) < 0.01f && (actualMax - safe.max).sqrMagnitude < 0.01f,
                "Safe-area fitting lost the resized window anchors.");
        }
    }

    static void Poll()
    {
        if (!SessionState.GetBool(StateKey, false)) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(StateKey + ".Start", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds > 90)
        {
            Finish("Debug console validation timed out.");
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (manager == null)
            {
                Application.logMessageReceived += CaptureError;
                var camera = new GameObject("Debug Console Validation Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePath));
                manager = instance.GetComponent<DebugLogManager>();
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("singleton").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                instance.SetActive(true);
                manager.ShowLogWindow();
                startFrame = Time.frameCount;
                step = 0;
                return;
            }
            if (Time.frameCount < startFrame + 5) return;
            Canvas.ForceUpdateCanvases();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (step == 0)
            {
                // The package's Start applies its initial minimized state.
                manager.ShowLogWindow();
                manager.ClearLogs();
                trace = "STACK_BEGIN\n角色加载失败：human/tetsuya\n" + new string('W', 22000) + "\nSTACK_END";
                manager.ReceivedLog("CONSOLE_PROBE: Could not prepare unit: 2", trace, LogType.Exception);
                manager.ReceivedLog("LONG_MESSAGE_PROBE: " + new string('W', 22000), "END_OF_LONG_MESSAGE", LogType.Warning);
                Require(manager.GetAllLogs().Contains("STACK_END"), "Full log export lost the end of the stack.");
                step = 1;
            }
            else if (step == 1)
            {
                var row = manager.GetComponentsInChildren<DebugLogItem>().Single(item => item.Entry != null && item.Entry.logString.StartsWith("CONSOLE_PROBE"));
                var text = row.GetComponentsInChildren<TMP_Text>().First(item => item.text.StartsWith("CONSOLE_PROBE"));
                Require(text.text.Length <= 200, "Collapsed long log was not bounded.");
                var longRow = manager.GetComponentsInChildren<DebugLogItem>().Single(item => item.Entry != null && item.Entry.logString.StartsWith("LONG_MESSAGE_PROBE"));
                Require(longRow.GetComponentsInChildren<TMP_Text>().First(item => item.text.StartsWith("LONG_MESSAGE_PROBE")).text.Length <= 200,
                    "Long message overflowed its collapsed row.");
                manager.GetComponentInChildren<DebugLogRecycledListView>().OnLogItemClicked(row);
                step = 2;
            }
            else if (step == 2)
            {
                var row = manager.GetComponentsInChildren<DebugLogItem>().Single(item => item.Expanded && item.Entry.logString.StartsWith("CONSOLE_PROBE"));
                var text = row.GetComponentsInChildren<TMP_Text>().First(item => item.text.StartsWith("CONSOLE_PROBE"));
                text.ForceMeshUpdate();
                Require(text.text.Contains("STACK_BEGIN") && text.text.Length <= 10000, "Expanded stack is unreadable or unbounded.");
                Require(text.textInfo.characterInfo.Any(character => character.isVisible), "Expanded row generated no visible characters.");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/expanded-stack.png"));
                var scroll = (ScrollRect)new SerializedObject(manager).FindProperty("logItemsScrollRect").objectReferenceValue;
                scroll.verticalNormalizedPosition = 0;
                step = 3;
            }
            else if (step == 3)
            {
                var row = manager.GetComponentsInChildren<DebugLogItem>().Single(item => item.Expanded && item.Entry.logString.StartsWith("CONSOLE_PROBE"));
                var copy = row.GetComponentsInChildren<Button>().Single(button => button.gameObject.name.Contains("Copy"));
                var scroll = (ScrollRect)new SerializedObject(manager).FindProperty("logItemsScrollRect").objectReferenceValue;
                var rect = (RectTransform)copy.transform;
                var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Require(copy.gameObject.activeInHierarchy && copy.interactable
                    && RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center),
                    "Copy button cannot be reached by scrolling the expanded log.");
                var clipboard = GUIUtility.systemCopyBuffer;
                try
                {
                    copy.onClick.Invoke();
                    Require(GUIUtility.systemCopyBuffer.Contains("STACK_END"), "Copy button lost the full stack trace.");
                }
                finally { GUIUtility.systemCopyBuffer = clipboard; }
                var canvas = manager.GetComponent<Canvas>();
                Require(canvas.pixelRect.width < canvas.pixelRect.height, "Console viewport is not portrait.");
                Require(manager.GetAllLogs().Contains("角色加载失败"), "CJK log text was lost.");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/copy-button.png"));
                step = 4;
            }
            else
            {
                Require(File.Exists(Output + "/expanded-stack.png") && File.Exists(Output + "/copy-button.png"), "Console previews were not captured.");
                Finish(null);
                return;
            }
            startFrame = Time.frameCount;
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    static void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }

    static void Finish(string failure)
    {
        SessionState.SetBool(StateKey, false);
        Application.logMessageReceived -= CaptureError;
        if (failure != null) errors.Add(failure);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(new Report
        {
            passed = errors.Count == 0, unityVersion = Application.unityVersion, packageVersion = "1.9.0",
            fullLogLength = trace?.Length ?? 0, screenWidth = manager == null ? 0 : Mathf.RoundToInt(manager.GetComponent<Canvas>().pixelRect.width),
            screenHeight = manager == null ? 0 : Mathf.RoundToInt(manager.GetComponent<Canvas>().pixelRect.height), errors = errors.ToArray()
        }, true));
        SessionState.SetBool(StateKey + ".Done", true);
        SessionState.SetInt(StateKey + ".Exit", errors.Count == 0 ? 0 : 1);
        if (errors.Count == 0) Debug.Log("Debug console validation passed: long log, expansion, copy and portrait safe area.");
        else Debug.LogError(string.Join("\n", errors));
        EditorApplication.ExitPlaymode();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

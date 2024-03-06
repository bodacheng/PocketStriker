using NoSuchStudio.Common.Service;
using System;
using UnityEditor;
using UnityEngine;

namespace NoSuchStudio.Common.Editor {
    public abstract class NoSuchEditor : UnityEditor.Editor {
        protected GUIStyle styleOn;
        protected GUIStyle styleOff;

        protected void DrawObjectType(GameObject go) {
            EditorGUILayout.BeginFoldoutHeaderGroup(true, new GUIContent("Stage Info", GameObjectTypeLogging.StageInformationStr(go)));
            EditorGUILayout.TextArea(GameObjectTypeLogging.StageInformationStr(go));
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.BeginFoldoutHeaderGroup(true, new GUIContent("Prefab Info", GameObjectTypeLogging.PrefabInformationStr(go)));
            EditorGUILayout.TextArea(GameObjectTypeLogging.PrefabInformationStr(go));
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        protected void DrawStage(GameObject go) {
            bool isMainStage = EditorUtilities.IsInMainStage(go);
            EditorGUILayout.LabelField("Stage", isMainStage ? "Main" : "Prefab", isMainStage ? styleOn : styleOff);
        }

        protected void DrawServiceConnectionStatus<ST>(IServiceComponent<ST> sc)
            where ST : Service<ST> {
            bool connected = sc.IsConnected<ST>();
            bool serviceReady = Service<ST>.IsReady;
            bool good = connected && serviceReady;
            string status = "Connected";
            if (!serviceReady) {
                status = "Service Unavailable";
            } else if (!connected) {
                status = "Disconnected";
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(typeof(ST).Name + " Connection");
            EditorGUILayout.LabelField(status, good ? styleOn : styleOff);
            EditorGUILayout.EndHorizontal();
        }

        protected string PrefKeyPrefix {
            get {
                return GetType().Name;
            }
        }

        public static void SetTextureColor(Texture2D tex2, Color32 color) {
            var fillColorArray = tex2.GetPixels32();
            for (var i = 0; i < fillColorArray.Length; ++i) {
                fillColorArray[i] = color;
            }
            tex2.SetPixels32(fillColorArray);
            tex2.Apply();
        }

        protected virtual void OnEnable() {
            styleOn = new GUIStyle();
            Texture2D tex = new Texture2D(2, 2);
            SetTextureColor(tex, new Color(0, 255, 0, 255));//r,g,b,a 
            // styleOn.normal.background = tex;
            styleOn.normal.textColor = new Color(0f, 0.5f, 0f, 1f);

            styleOff = new GUIStyle();
            tex = new Texture2D(2, 2);
            SetTextureColor(tex, new Color(255, 0, 0, 255));//r,g,b,a 
            //styleOff.normal.background = tex;
            styleOff.normal.textColor = new Color(0.6f, 0f, 0f, 1f);
        }

        public Logger logger {
            get {
                Type thisType = GetType();
                return UnityObjectLoggerExt.GetLoggerByType(thisType).logger;
            }
        }
        public LoggerConfig loggerConfig {
            get {
                Type thisType = GetType();
                return UnityObjectLoggerExt.GetLoggerByType(thisType).loggerConfig;
            }
        }

        protected void LogLogFormat(string format, params object[] args) {
            UnityObjectLoggerExt.LogLogFormat(this, format, args);
        }
        protected void LogLog(string log) {
            UnityObjectLoggerExt.LogLog(this, log);
        }
        protected void LogWarnFormat(string format, params object[] args) {
            UnityObjectLoggerExt.LogWarnFormat(this, format, args);
        }
        protected void LogWarn(string log) {
            UnityObjectLoggerExt.LogWarn(this, log);
        }
        protected void LogErrorFormat(string format, params object[] args) {
            UnityObjectLoggerExt.LogErrorFormat(this, format, args);
        }
        protected void LogError(string log) {
            UnityObjectLoggerExt.LogError(this, log);
        }

        public static void LogLogFormat<T>(string format, params object[] args) {
            UnityObjectLoggerExt.LogLogFormat<T>(format, args);
        }
        public static void LogLog<T>(string log) {
            UnityObjectLoggerExt.LogLog<T>(log);
        }
        public static void LogWarnFormat<T>(string format, params object[] args) {
            UnityObjectLoggerExt.LogWarnFormat<T>(format, args);
        }
        public static void LogWarn<T>(string log) {
            UnityObjectLoggerExt.LogWarn<T>(log);
        }
        public static void LogErrorFormat<T>(string format, params object[] args) {
            UnityObjectLoggerExt.LogErrorFormat<T>(format, args);
        }
        public static void LogError<T>(string log) {
            UnityObjectLoggerExt.LogError<T>(log);
        }

        public static void LogLogFormat<T>(UnityEngine.Object unityObj, string format, params object[] args) {
            UnityObjectLoggerExt.LogLogFormat<T>(unityObj, format, args);
        }
        public static void LogLog<T>(UnityEngine.Object unityObj, string log) {
            UnityObjectLoggerExt.LogLog<T>(unityObj, log);
        }
        public static void LogWarnFormat<T>(UnityEngine.Object unityObj, string format, params object[] args) {
            UnityObjectLoggerExt.LogWarnFormat<T>(unityObj, format, args);
        }
        public static void LogWarn<T>(UnityEngine.Object unityObj, string log) {
            UnityObjectLoggerExt.LogWarn<T>(unityObj, log);
        }
        public static void LogErrorFormat<T>(UnityEngine.Object unityObj, string format, params object[] args) {
            UnityObjectLoggerExt.LogErrorFormat<T>(unityObj, format, args);
        }
        public static void LogError<T>(UnityEngine.Object unityObj, string log) {
            UnityObjectLoggerExt.LogError<T>(unityObj, log);
        }
    }
}
using UnityEditor;
using UnityEditor.SearchService;

namespace Cocone.ProjectP3
{
    /**
     * Configuration.assetを変更するためのクラス
     */
    public static class ConfigurationSetter
    {
        private const string appCenterAssetPath = "Assets/AppCenter/AppCenterSettings.asset";

        
        /// <summary>
        /// AppCenterの設定を環境別に変更する
        /// </summary>
        /// <param name="distribute"></param>
        public static void SetAppCenterParam(bool distribute)
        {
            var appCenterSettings = AssetDatabase.LoadAssetAtPath<AppCenterSettings>(appCenterAssetPath);

            appCenterSettings.UseDistribute = distribute;
            appCenterSettings.EnableDistributeForDebuggableBuild = distribute;

            EditorUtility.SetDirty(appCenterSettings);
        }
    }
}
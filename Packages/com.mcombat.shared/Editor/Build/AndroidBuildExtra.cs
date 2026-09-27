using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class AndroidBuildExtra : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    // Android特殊处理
    string mainTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";
    string backupPath = "Assets/Plugins/Android/mainTemplate.gradle.backup";

    public void OnPreprocessBuild(BuildReport report)
    {
        // Backup mainTemplate.gradle
        if (File.Exists(mainTemplatePath))
        {
            File.Copy(mainTemplatePath, backupPath, true);
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, mainTemplatePath, true);
        }
    }
}

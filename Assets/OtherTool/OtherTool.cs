#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Linq;

public partial class OtherTool : EditorWindow
{
    [SerializeField] Shader ChangeShader;
    [SerializeField] Shader ToShader;
    
    string[] unitTypes = {"human"};
    TextAsset unitConfigFile;
    string unitConfigFilePath;
    TextAsset SkillConfigFile;
    string SkillConfigPath;
    
    void OnGUI()
    {
        ChangeShader = EditorGUILayout.ObjectField("Change Shader", ChangeShader, typeof(Shader), true) as Shader;
        ToShader = EditorGUILayout.ObjectField("To Shader", ToShader, typeof(Shader), true) as Shader;

        
        if (GUILayout.Button("置换一切材质的shader"))
        {
            var list = AssetDatabase
                .FindAssets( "t:Material" )
                .Select( AssetDatabase.GUIDToAssetPath )
                .Select( AssetDatabase.LoadAssetAtPath<Material> )
                .Where( c => c != null );
            foreach (var m in list)
            {
                if (m.shader == ChangeShader)
                {
                    m.shader = ToShader;
                }
            }
        }
        
        #region 暂时不再使用

        // unitConfigFile = EditorGUILayout.ObjectField("角色定义文件", unitConfigFile, typeof(TextAsset), true) as TextAsset;
        // unitConfigFilePath = EditorGUILayout.TextField("角色定义文件路径",unitConfigFilePath);
        // SkillConfigFile = EditorGUILayout.ObjectField("技能定义文件", SkillConfigFile, typeof(TextAsset), true) as TextAsset;
        // SkillConfigPath = EditorGUILayout.TextField("技能定义文件",SkillConfigPath);
        
        // 技能和角色的添加，手动就够了，所以以下函数comment out。
        
        if (GUILayout.Button("根据Resource文件夹生成所有角色配置文件"))
        {
            UnitsConfigFileGenerate(unitConfigFilePath, unitConfigFile, unitTypes);
        }
        if (GUILayout.Button("根据Resource文件夹生成,更新技能配置文件"))
        {
            SkillConfigFileUpdate(SkillConfigPath, SkillConfigFile, unitTypes);
        }
        
        if (GUILayout.Button("全项目所有贴图转换iphone格式"))
        {
            Debug.Log("危险");
            var guids = AssetDatabase.FindAssets("t:texture2D",  null);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter != null)
                {
                    Debug.Log("尝试更改以下贴图设置："+ path);
                    TextureImporterPlatformSettings iPhone_png = new TextureImporterPlatformSettings
                    {
                        overridden = true,
                        name = "iPhone",
                        maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_4x4,
                        compressionQuality = 50,
                        allowsAlphaSplitting = false
                    };
        
                    TextureImporterPlatformSettings iPhone_jpeg = new TextureImporterPlatformSettings
                    {
                        overridden = true,
                        name = "iPhone",
                        maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_4x4,
                        compressionQuality = 50,
                        allowsAlphaSplitting = false
                    };
        
                    TextureImporterPlatformSettings Android_png = new TextureImporterPlatformSettings
                    {
                        overridden = false,
                        name = "Android",
                        maxTextureSize = 2048,
                        //format = TextureImporterFormat.DXT5,
                        compressionQuality = 50,
                        allowsAlphaSplitting = false
                    };
        
                    TextureImporterPlatformSettings Android_jpeg = new TextureImporterPlatformSettings
                    {
                        overridden = false,
                        name = "Android",
                        maxTextureSize = 2048,
                        //format = TextureImporterFormat.ETC2_RGBA8,
                        compressionQuality = 50,
                        allowsAlphaSplitting = false
                    };
        
                    if (textureImporter.DoesSourceTextureHaveAlpha ()) {
                        //Alphaチャンネルあある場合
                        //textureImporter.SetPlatformTextureSettings (iPhone_png);
                        textureImporter.SetPlatformTextureSettings (Android_png);
                    } else {
                        //Alphaチャンネルがない場合
                        //textureImporter.SetPlatformTextureSettings (iPhone_jpeg);
                        textureImporter.SetPlatformTextureSettings (Android_jpeg);
                    }
                }
            }
        }

        #endregion
    }
}
#endif

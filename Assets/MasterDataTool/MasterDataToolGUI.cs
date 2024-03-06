#if UNITY_EDITOR

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public class LocalMasterDataToolGUI : EditorWindow {

    bool Initialized;
    private readonly MasterDataTool tool = new MasterDataTool();
    
    void OnGUI()
    {
        if (!Initialized)
        {
            Initialized = true;
        }
        
        GUILayout.TextArea(" 输出技能参考信息文件，这个本程序的技能石详细画面要参考的。\n" +
                           "但输出的内容只是个大概。\n");
        
        if (GUILayout.Button("输出最新技能数值参考文件（技能详细画面用）目前需要play模式下执行"))
        {
            PowerEstimateTable.Save("human").Forget();
        }

        GUILayout.TextArea(" PlayFab相关文件，可以在PlayFab通过Update Json来实现对Master数据的更新，\n" +
                           "但是大部分情况下其实是没有什么用的，因为技能石也好，角色也好，我们可以选择谨慎的手动更新。\n" +
                           "而商店文件这个东西不知道我们当初为什么要给开发出来，其实PlayFab的商店功能我们应该是用不到。\n" +
                           "另外最大的重点：这些出力功能都是基于Unity客户端下的技能和角色定义文件的。");
        
        if (GUILayout.Button("(playFab)输出Json格式技能石定义文件"))
        {
            tool.OutputSKStonesCatalog();
        }

        if (GUILayout.Button("(playFab)输出Json格式技能石商店文件"))
        {
            tool.OutputSKStonesStore();
        }
        
        if (GUILayout.Button("(playFab)输出Json格式角色定义文件"))
        {
            tool.OutputMonstersCatalog();
        }

        if (GUILayout.Button("(playFab)输出Json格式角色商店文件"))
        {
            tool.OutputMonsterStore();
        }

        GUILayout.TextArea("关卡报酬信息我们保存在PlayFab->TITLE DATA里。\n" +
                           "要注意的一点是这个部分我们没法在PlayFab直接使用Upload Json 来更新内容。\n" +
                           "我们是点编辑，把值直接copy到stage_awards这个key对应的值那里。\n" +
                           "下面这个按钮输出的文件里是那个值，只是提供了个格式，详细我们可以自己编辑，或在未来改写代码。");
        
        if (GUILayout.Button("(playFab)输出Json格式Arcade关卡报酬定义文件"))
        {
            StageEditor.ExportStageAward();
        }
    }
}
#endif
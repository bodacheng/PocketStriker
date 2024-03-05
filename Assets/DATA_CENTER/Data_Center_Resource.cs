using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Data_Center : MonoBehaviour
{
    // public IEnumerator Step2InitializeByResourceFolder(string type, TextAsset Script, Element element, string personalMagic)
    // {
    //     if (!Phase2Initialized)
    //     {
    //         Phase2Initialized = true;
    //     }
    //
    //     if (_MyBehaviorRunner.usingScript != Script)
    //     {
    //         _MyBehaviorRunner.LoadStatesTransition(Script);
    //         // 上面这个环节结束后，有这样几个重要情况1. state_Transition_Dictionary的内容就正确了 2.AIStateRunner内的States_Dictionary实例内将有一份正确的skill类key的列表
    //         _MyBehaviorRunner.INIStates(this);
    //         var toLoadSkillAnimsNames = _MyBehaviorRunner.PassSkillTypeKeys();
    //         yield return (AnimationManger.PreloadPersonalAnimsResourceMode(type, toLoadSkillAnimsNames, element));
    //         //上面函数里隐藏着一个相当大头的工作，那就是提前根据动画片段生成所有可能的对象池，牵扯到各种路径的确定，BO_E必须提前做好准备
    //     }
    //
    //     yield return null;
    // }
}

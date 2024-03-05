using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Serialization;
using Soul;
using Skill;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class AIScriptReading {

    public static List<SkillEntity> ReadKongfuBook(BehaviorRunner _AIStateRunner,TextAsset Script)
    {
        try
        {
            List<SkillEntity> list = new List<SkillEntity>();
            XmlSerializer serializer = new XmlSerializer(typeof(List<SkillEntity>));
            
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
            {
                //FileStream FileStream = new FileStream(Application.dataPath + pathAndFileName, FileMode.Open);
                //list = XmlSerializer.Deserialize(FileStream) as List<State_Transition_Set>;
                //FileStream.Close();
                #if UNITY_EDITOR
                string _path = AssetDatabase.GetAssetPath(Script);
                string[] pathsplit = _path.Split(new string[] { "Assets" }, StringSplitOptions.None);
                _path = _path.Length > 1 ? pathsplit[1] : pathsplit[0];
                _AIStateRunner.AI_States_path = _path;
                #endif
                
                using (TextReader textReader = new StringReader(Script.text))
                {
                    list = serializer.Deserialize(textReader) as List<SkillEntity>;
                }
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                var reader = new System.IO.StringReader(Script.text);
                list = serializer.Deserialize(reader) as List<SkillEntity>;
            }
            list = _AIStateRunner.SortStateTransitionSetList(list);
            _AIStateRunner.usingScript = Script;
            if (list == null)
            {
                list = new List<SkillEntity>() {new SkillEntity("Empty",0, new AIAttrs(), null ,null,InputKey.Null, InputKey.Null,0)};
            }else{
                if (list.Count == 0)
                {
                    list.Add(new SkillEntity("Empty",0, new AIAttrs(),null,null,InputKey.Null, InputKey.Null,0));
                }
            }
            return list;
        }
        catch (NullReferenceException e)
        {
            Debug.Log("状态迁移信息读取失败,返回只有空状态的列表");
            Debug.Log(e.ToString());
            return new List<SkillEntity>() {new SkillEntity("Empty",0,new AIAttrs(),null,null,InputKey.Null, InputKey.Null,0)};
        }
    }
}

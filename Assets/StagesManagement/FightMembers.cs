using UnityEngine;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class FightMembers
{
    [NonSerialized] public MultiDic<int, int, UnitInfo> HeroSets = new MultiDic<int, int, UnitInfo>();
    public MultiDic<int, int, UnitInfo> EnemySets = new MultiDic<int, int, UnitInfo>();
    
    public static bool TeamLegal(MultiDic<int, int, UnitInfo> team, List<string> checkInstanceIds = null)
    {
        bool legal = team.GetValues().Count > 0;
        if (!legal)
            return false;
        foreach (var unit in team.GetValues())
        {
            if (checkInstanceIds != null && !checkInstanceIds.Contains(unit.id))
                continue;
            legal = unit.set.CheckEdit() == SkillSet.SkillEditError.Perfect && legal;
        }
        return legal;
    }
    
    public bool CheckStonesLegal(FightEventType eventType, List<string> checkInstanceIds = null)
    {
        switch (eventType)
        {
            case FightEventType.Quest:
            case FightEventType.Gangbang:
                return TeamLegal(HeroSets, checkInstanceIds);//checkInstanceIds 是针对Gangbang的
            default:
                return TeamLegal(HeroSets) && TeamLegal(EnemySets);
        }
    }
    
    public FightMembers()
    {
    }
    
    public void SetEnemyLevel(float level)
    {
        foreach (var unitInfo in EnemySets.GetValues())
        {
            if (unitInfo != null)
                unitInfo.level = level;
        }
    }
    
    static UnitInfo ArrangeUnitInfo(string type, string unitRecordID)
    {
        var skillId = UnitPassiveTable.GetUnitPassiveRecordId(unitRecordID);
        var unitInfo = new UnitInfo
        {
            r_id = unitRecordID,
            set = SkillSet.RandomSkillSet(type, skillId,false)
        };
        return unitInfo;
    }
    
    public static FightMembers RandomFight()
    {
        var type = "human";
        
        var unitIDsAndNames = Units.GetMonsterIDsAndNamesDic(type);
        var indexes = RandomSelect.Get(0, unitIDsAndNames.Count - 1, 3);
        var recordIds = unitIDsAndNames.Keys.ToList();
        var target = new FightMembers();
        
        var char1 = ArrangeUnitInfo(type, recordIds[indexes[0]]);
        var char2 = ArrangeUnitInfo(type, recordIds[indexes[1]]);
        var char3 = ArrangeUnitInfo(type, recordIds[indexes[2]]);
        
        target.EnemySets.Set(0, 0, char1);
        target.EnemySets.Set(0, 1, char2);
        target.EnemySets.Set(0, 2, char3);
        
        return target;
    }
    
    public static FightMembers RandomSkillTest(TeamMode teamMode)
    {
        var type = "human";
        var unitIDsAndNames = Units.GetMonsterIDsAndNamesDic(type);
        var indexes = RandomSelect.Get(0, unitIDsAndNames.Count - 1, 12);
        var recordIds = unitIDsAndNames.Keys.ToList();
        var target = new FightMembers();
        var char1 = ArrangeUnitInfo(type, recordIds[indexes[0]]);
        var char2 = ArrangeUnitInfo(type, recordIds[indexes[1]]);
        var char3 = ArrangeUnitInfo(type, recordIds[indexes[2]]);
        var char4 = ArrangeUnitInfo(type, recordIds[indexes[3]]);
        var char5 = ArrangeUnitInfo(type, recordIds[indexes[4]]);
        var char6 = ArrangeUnitInfo(type, recordIds[indexes[5]]);
        
        var char7 = ArrangeUnitInfo(type, recordIds[indexes[6]]);
        var char8 = ArrangeUnitInfo(type, recordIds[indexes[7]]);
        var char9 = ArrangeUnitInfo(type, recordIds[indexes[8]]);
        var char10 = ArrangeUnitInfo(type, recordIds[indexes[9]]);
        var char11 = ArrangeUnitInfo(type, recordIds[indexes[10]]);
        var char12 = ArrangeUnitInfo(type, recordIds[indexes[11]]);

        target.EnemySets.Set(0, 0, char1);
        target.EnemySets.Set(0, 1, char2);
        target.EnemySets.Set(0, 2, char3);
        target.EnemySets.Set(0, 3, char7);
        target.EnemySets.Set(0, 4, char8);
        target.EnemySets.Set(0, 5, char9);
        
        target.HeroSets.Set(0, 0, char4);
        target.HeroSets.Set(0, 1, char5);
        target.HeroSets.Set(0, 2, char6);
        target.HeroSets.Set(0, 3, char10);
        target.HeroSets.Set(0, 4, char11);
        target.HeroSets.Set(0, 5, char12);
        
        return target;
    }
    
    public static FightMembers ScreenSaver(TeamMode teamMode)
    {
        var type = "human";
        var target = new FightMembers();
        var char1 = ArrangeUnitInfo(type, "1");
        var char2 = ArrangeUnitInfo(type, "2");
        switch (teamMode)
        {
            case TeamMode.MultiRaid:
                target.EnemySets.Set(0, 0, char1);
                target.HeroSets.Set(0, 0, char2);
                break;
            case TeamMode.Rotation:
                target.EnemySets.Set(0, 0, char1);
                target.HeroSets.Set(0, 0, char2);
                break;
        }
        return target;
    }
    
    public static FightMembers LoadEnemies_Json(TextAsset Script)
    {
        var _localFight = new FightMembers();
        try
        {
            var targetValue = JsonConvert.DeserializeObject<MultiDic<int, int, UnitInfo>.SerializableSet[]>(Script.text);
            _localFight.EnemySets._SerializableSets = targetValue;
            _localFight.EnemySets.ConvertSerializableArrayToDictionary();
            return _localFight;
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return null;
        }
    }
    
    public void SaveFightAsXml(string path, FightMembers localFight)
    {
        if (localFight == null)
        {
            return;
        }
        MultiDic<int, int, UnitInfo> UnNullDic = new MultiDic<int, int, UnitInfo>();
        foreach (MultiDic<int, int, UnitInfo>.SerializableSet sets in localFight.EnemySets._SerializableSets)
        {
            UnNullDic.Set(sets.key1, sets.key2 , sets.value);
        }

        try
        {
            var XmlSerializer = new XmlSerializer(typeof(MultiDic<int, int, UnitInfo>.SerializableSet[]));
            var FileStream = new FileStream(Application.dataPath + "/" + path, FileMode.Create);
            XmlSerializer.Serialize(FileStream, UnNullDic._SerializableSets);
            Debug.Log(Application.dataPath + path + " 尝试进行关卡存储");
            FileStream.Close();
        }
        catch (Exception e)
        {
            Debug.Log("战斗信息保存失败");
            Debug.Log(e.ToString());
        }
    }
    
    public FightMembers LoadOneLocalFight_XML(TextAsset Script)
    {
        var _localFight = new FightMembers();
        
        MultiDic<int, int, UnitInfo>.SerializableSet[] targetValue;
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(MultiDic<int, int, UnitInfo>.SerializableSet[]));
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
            {
                //FileStream FileStream = new FileStream(Application.dataPath + pathAndFileName, FileMode.Open);
                //list = XmlSerializer.Deserialize(FileStream) as List<State_Transition_Set>;
                //FileStream.Close();
                using (TextReader textReader = new StringReader(Script.text))
                {
                    targetValue = serializer.Deserialize(textReader) as MultiDic<int, int, UnitInfo>.SerializableSet[];
                }
                Debug.Log("读取了敌人战斗信息");
            }
            else
            {
                var reader = new StringReader(Script.text);
                targetValue = serializer.Deserialize(reader) as MultiDic<int, int, UnitInfo>.SerializableSet[];
                Debug.Log("读取了敌人战斗信息");
            }
            
#if UNITY_EDITOR
            string _path = AssetDatabase.GetAssetPath(Script);
            string[] pathsplit = _path.Split(new string[] { "Assets" }, StringSplitOptions.None);
            _path = _path.Length > 1 ? pathsplit[1] : pathsplit[0];
            Debug.Log("4V4模式文件" + _path);
#endif
            
            _localFight.EnemySets._SerializableSets = targetValue;
            _localFight.EnemySets.ConvertSerializableArrayToDictionary();
            return _localFight;
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return null;
        }
    }
}
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Skill;
using Log;

public partial class OtherTool : EditorWindow
{
    // 2022.5.8.
    // 这个函数我认为已经没有什么用了，因为预计我们这个游戏加入的角色不会太多，
    // 而且每次加入的时候会很谨慎，所以手动在定义文件加入对应条目就足够了。
    // 假设有一天这个函数真有用的话那可以读读下面的介绍。真是太可笑了，我们竟然以为自己能驾驭的了那么多复杂的企划。
    
    // 以下这些是很久以前写的介绍：
    // 由Resource文件夹更新角色配置文件信息所需要的工作应该有如下：
    // 首先，同prefabName 允许在数据库存在复数个条目。比如外观一样的红色魔法暴龙和蓝色魔法暴龙，他们可以realName一样但recordId不同。
    // 但首次自动生成配置文件，系统只会为新的资源添加一条对应条目，并且条目具体信息是默认的，非常空，一定需要手动设置。
    // 如果要有一样的资源不同的条目这种情况，必定是手动添加的结果。
    // 如果数据库里存在条目在Resource下检测不到对应资源。。。。那这样的条目会被删除。原先的ID会被新添加的资源对应的新条目补空档。
    // 系统不会对旧条目中自己手动填写的任何具体定义做更新，只会根据资源的有无来决定条目的追加与删除与否。
    public void UnitsConfigFileGenerate(string path, TextAsset textAsset, string[] chartypes)
    {
        if (textAsset != null)
        {
            Units.Load(textAsset);
        }
        var AllDeletedRecordsIDs = new List<int>();
        var existedUnitUnitRID = new List<string>();
        var AllNewUnitConfigsAllTypes = new List<UnitConfig>();
        foreach (string type in chartypes)
        {
            List<string> currentAllRealNamesOfResourceFolder = new List<string>();
            List<UnitConfig> unitConfigsOfOldConfigFileOFtype = Units.RowToConfigList(Units.FindAll_MONSTER_TYPE(type));
            List<string> keySonnCharacterRealNames = new List<string>();
            foreach (var oneConfig in unitConfigsOfOldConfigFileOFtype)
            {
                if (!keySonnCharacterRealNames.Contains(oneConfig.REAL_NAME))
                {
                    keySonnCharacterRealNames.Add(oneConfig.REAL_NAME);
                }
                else
                {
                    //什么也不做。允许。
                }
                existedUnitUnitRID.Add(oneConfig.RECORD_ID);
            }

            Object[] pretabResources = Resources.LoadAll("CharPretabs/" + type);
            foreach (Object charPretab in pretabResources)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(charPretab.name))
                    currentAllRealNamesOfResourceFolder.Add(charPretab.name);

                if (keySonnCharacterRealNames.Contains(charPretab.name))
                {
                    continue;//不对原来就存在的资源对应条目做更改。
                }

                GameObject character = charPretab as GameObject;
                if (character.GetComponent<OutsideDataLink>() == null)
                {
                    Debug.Log(type + "资源" + charPretab.name + "丢失必要组件，不是一个正常角色资源");
                    continue;
                }

                UnitConfig unitConfig = new UnitConfig
                {
                    RECORD_ID = "-1",
                    TYPE = type,
                    REAL_NAME = charPretab.name,
                    showNameEN = null,
                    showNameCN = null,
                    showNameJP = null
                };

                OutsideDataLink outsideDataLink = character.GetComponent<OutsideDataLink>();
                switch (outsideDataLink._C.element)
                {
                    case Element.blueMagic:
                        unitConfig.element = Element.blueMagic;
                        break;
                    case Element.redMagic:
                        unitConfig.element = Element.redMagic;
                        break;
                    case Element.greenMagic:
                        unitConfig.element = Element.greenMagic;
                        break;
                    case Element.darkMagic:
                        unitConfig.element = Element.darkMagic;
                        break;
                    case Element.lightMagic:
                        unitConfig.element = Element.lightMagic;
                        break;
                }
                
                unitConfig.BASIC_MOVEMENT_PACK = "warrior";//我感觉这个应该起名字叫做basic。每个type起码有一个叫这个的。
                unitConfig.MoveType = MoveType.Move_normal;
                unitConfig.RushType = RushType.RushBack;
                unitConfig.DEFENDABLE_FLAG = true;
                unitConfig.InstructionCH = null;
                unitConfig.InstructionEN = null;
                unitConfig.InstructionJP = null;
                unitConfig.RARITY_LEVEL = 1;

                AllNewUnitConfigsAllTypes.Add(unitConfig);
            }

            //旧版本有的keyname可是Resource文件夹下没有的
            List<string> ResourceNamesShouldDeletedFromConfig = new List<string>();
            foreach (string keyname in keySonnCharacterRealNames)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(keyname))
                    ResourceNamesShouldDeletedFromConfig.Add(keyname);
            }

            foreach (string keyname in ResourceNamesShouldDeletedFromConfig)
            {
                List<Units.Row> toDeleteRows = Units.FindAll_TYPE_REALNAME(type, keyname);
                foreach (Units.Row row in toDeleteRows)
                {
                    if (!AllDeletedRecordsIDs.Contains(int.Parse(row.RECORD_ID)))
                    {
                        Debug.Log("这是一个要删除的ID" + int.Parse(row.RECORD_ID));
                        AllDeletedRecordsIDs.Add(int.Parse(row.RECORD_ID));
                    }
                    else
                        Debug.Log("原monstersConfigTable似乎有重复ID，而且似乎还是因为资源缺失要删除的条目。。");
                    Units.rowList.Remove(row);
                }
            }
        }

        foreach (UnitConfig characterResourceInfo in AllNewUnitConfigsAllTypes)
        {
            if (AllDeletedRecordsIDs.Count > 0)
            {
                characterResourceInfo.RECORD_ID = "new";
                Debug.Log(characterResourceInfo.REAL_NAME + "的 ID： " + characterResourceInfo.RECORD_ID);
                AllDeletedRecordsIDs.RemoveAt(0);
                existedUnitUnitRID.Add(characterResourceInfo.RECORD_ID);
            }
            else
            {
                characterResourceInfo.RECORD_ID = "new";
                existedUnitUnitRID.Add(characterResourceInfo.RECORD_ID);
            }
            Units.Row newRow = Units.ConfigToRow(characterResourceInfo);
            if (newRow != null && newRow.REAL_NAME != null)
                Units.rowList.Add(newRow);
        }
        Units.SaveByCurrentRows(Application.dataPath + "/" + path != null ? path : CommonSetting.UnitConfigFile);
    }
    
    // 以下这个函数对技能表的更新机制企划如下：
    // 首先读取现有配置文件，获取现有的所有条目。然后，读取resource文件夹，会按type顺序拿现有条目和resource进行比较。
    // 配置文件允许在一个type下存在相同realname的复数个条目(同一个动画不同攻击类型)，
    // 但为了管理思路清晰我们不允许Resource文件夹下出现同名资源，确切的说不允许一个type下，某一个攻击类型的动画片段在另一个攻击类型的文件夹里有同名动画片段
    // 一旦发现上述情况将中断配置文件更新。
    // 如检测出现有的配置文件中记载的某一个条目的keyname值找不到对应type的Resource文件夹资源，那么这个条目会被记载下来进行删除。
    // 如果有复数个同keyname条目找不到对应type的Resource文件夹资源，这几个条目都会被删除。
    // 上述情况下被删除的条目，他们原先的ID会被记录下来，然后就是重点：如果有新的条目加入，新条目会顶替被删除的旧条目换上这些ID
    // 而新条目的自动追加原则是：查看Resource文件夹下有没有配置文件里所有条目不存在的keyname(对应type下)，有这样的新资源，就给配置文件追加一个条目。
    // 1. 配置文件中如果同type下一个keyname有复数个条目，只能是我们手动所为 
    // 2. 配置文件里没“基本状态”的定义，只有技能状态定义。基本状态是角色加载时候函数给根据九宫格记载给添加的，不参考数据库或配置文件。
    // 3.如果一个keyname的资源，已经在相应type下的配置文件信息中有一个条目，那么，假设你更改了这个资源在Resource文件夹下的位置，比如从GR移动到了GM，
    // 那么系统将不会根据它在Resource文件夹下位置的变动来更新它在配置文件对应条目里的信息，原来是怎么定义的就是怎么定义的，除非找不到这个资源把它条目删了。
    // 也就是说这个配置文件更新函数的重点只是根据资源的有无来决定是不是添加初始条目或删除旧条目，技能详细定义你自己去改，它不会胡乱给你改。
    // 4. ID的“填补机制”是建立在原有条目对应资源缺失,并且在一次更新操作的前提下。只有那些找不到资源了的条目的旧ID才会被新资源对应的新条目代替ID。
    // 假设你在某资源存在的时候删了它条目，然后重新更新一次配置文件，会发现被补上的条目ID是最新（最大）值。这一点无论是角色Config还是SkillConfig都是一样的。

    public void SkillConfigFileUpdate(string path, TextAsset textAsset, string[] chartypes)
    {
        if (textAsset != null)
        {
            SkillConfigTable.Load(textAsset);
        }

        List<string> KisoonRecordIDs = new List<string>();
        List<string> AllDeletedRecordIDs = new List<string>();
        List<SkillConfig> AllNewSkillConfigsOfAllTypes = new List<SkillConfig>();

        foreach (string chartype in chartypes)
        {
            List<SkillConfig> SkillConfigsOfOldFileOFtype = SkillConfigTable.RowsToSkillConfigList(SkillConfigTable.FindAll_MONSTER_TYPE(chartype));
            List<string> KisonnRecourdsOFRealNames = new List<string>(); //这个type的角色旧config文件中所有条目的keyname list。
            foreach (SkillConfig skillConfig in SkillConfigsOfOldFileOFtype)
            {
                if (!KisonnRecourdsOFRealNames.Contains(skillConfig.REAL_NAME))
                {
                    KisonnRecourdsOFRealNames.Add(skillConfig.REAL_NAME);//同一个REAL_NAME在数据库可能不止一个条目。比如一个发波动画定义了两种攻击
                }

                if (!KisoonRecordIDs.Contains(skillConfig.RECORD_ID))
                {
                    KisoonRecordIDs.Add(skillConfig.RECORD_ID);
                }
                else
                {
                    Debug.Log("极端致命错误：原SkillConfigTable似乎有重复RECORD_ID,操作中止。");
                    return;
                }
            }

            //这个首先是确保了本地资源重名的情况下不会被重复登陆
            List<string> currentAllRealNamesOfResourceFolder = new List<string>();
            //接下来索引该type现在resource文件夹下的资源。如果是已经存在的
            List<SkillConfig> newSkillConfigsOfType = new List<SkillConfig>();

            // 下面这一大堆针对各个攻击片段文件夹的循环，意思是说，原则上一发现一个新动画片段，那么只针对它添加一个技能条目。如果你想针对一个动画片段添加其他类型的攻击技能那只能手动
            // 并且如果旧的config文件中已经有针对一个动画资源的技能状态定义，那么以旧定义为准，不会去动它，即便你换了某个动画片段的Resource文件夹位置。
            // 你如果真换了某个动画片段的Resource文件夹位置，那意味着你可能本来觉得它是个GR类攻击，那后来觉得做GM攻击更合适，那你只能手动去数据库文件做相应更改，这个更新操作不会替你做这个事情。
            // GR系列的状态角色必须有个叫做“dash”的冲刺动作
            Object[] GAttackStateResources = Resources.LoadAll("Animations/" + chartype + "/G_Attack_State", typeof(AnimationClip));
            foreach (Object _anim in GAttackStateResources)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(_anim.name))
                {
                    currentAllRealNamesOfResourceFolder.Add(_anim.name);
                }
                else
                {
                    Debug.Log("本地" + chartype + "type" + "出现重名动画资源。资源名：" + _anim.name + "，考虑改资源文件名。skill表更新操作中断。");
                    return;
                }

                if (!KisonnRecourdsOFRealNames.Contains(_anim.name))
                {
                    SkillConfig OneConfig = new SkillConfig
                    {
                        RECORD_ID = null,
                        TYPE = chartype,
                        REAL_NAME = _anim.name,
                        ATTACK_WEIGHT = 10,
                        SHOW_NAME = "unknown",
                        SP_LEVEL = 0,
                        STATE_TYPE = BehaviorType.GR,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = 0.2f,
                            AI_MAX_DIS = 5f,
                            height = 0
                        }
                    };
                    newSkillConfigsOfType.Add(OneConfig);
                }
                else
                {
                    //这个资源对应的条目已经在原先的config文件里已经有了，保留原先设定。
                }
            }

            Object[] GAttackStateStayResources = Resources.LoadAll("Animations/" + chartype + "/G_Attack_State_Stay", typeof(AnimationClip));
            foreach (Object _anim in GAttackStateStayResources)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(_anim.name))
                {
                    currentAllRealNamesOfResourceFolder.Add(_anim.name);
                }
                else
                {
                    Debug.Log("本地" + chartype + "type" + "出现重名动画资源。资源名：" + _anim.name + "，考虑改资源文件名。skill表更新操作中断。");
                    return;
                }

                if (!KisonnRecourdsOFRealNames.Contains(_anim.name))
                {
                    SkillConfig OneConfig = new SkillConfig
                    {
                        RECORD_ID = null,
                        TYPE = chartype,
                        REAL_NAME = _anim.name,
                        ATTACK_WEIGHT = 10,
                        SHOW_NAME = "unknown",
                        SP_LEVEL = 0,
                        STATE_TYPE = BehaviorType.GI,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = 3f,
                            AI_MAX_DIS = 10f,
                            height = 0
                        }
                    };
                    newSkillConfigsOfType.Add(OneConfig);
                }
                else
                {
                    //这个资源对应的条目已经在原先的config文件里已经有了，保留原先设定。
                }
            }

            Object[] GMResources = Resources.LoadAll("Animations/" + chartype + "/GMStates", typeof(AnimationClip));
            foreach (Object _anim in GMResources)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(_anim.name))
                {
                    currentAllRealNamesOfResourceFolder.Add(_anim.name);
                }
                else
                {
                    Debug.Log("本地" + chartype + "type" + "出现重名动画资源。资源名：" + _anim.name + "，考虑改资源文件名。skill表更新操作中断。");
                    return;
                }

                if (!KisonnRecourdsOFRealNames.Contains(_anim.name))
                {
                    SkillConfig OneConfig = new SkillConfig
                    {
                        RECORD_ID = null,
                        TYPE = chartype,
                        REAL_NAME = _anim.name,
                        ATTACK_WEIGHT = 10,
                        SHOW_NAME = "unknown",
                        SP_LEVEL = 0,
                        STATE_TYPE = BehaviorType.GM,
                        AIAttrs = new AIAttrs
                        {
                            AI_MIN_DIS = 3f,
                            AI_MAX_DIS = 8f,
                            height = 0
                        }
                    };
                    newSkillConfigsOfType.Add(OneConfig);
                }
            }

            AllNewSkillConfigsOfAllTypes.AddRange(newSkillConfigsOfType);
            List<string> ShouldDeletedFromConfig = new List<string>();//旧版本有的keyname可是Resource文件夹下没有的
            foreach (string realname in KisonnRecourdsOFRealNames)
            {
                if (!currentAllRealNamesOfResourceFolder.Contains(realname))
                {
                    ShouldDeletedFromConfig.Add(realname);
                }
            }
            foreach (string realname in ShouldDeletedFromConfig)
            {
                List<SkillConfigTable.Row> toDeleteRows = SkillConfigTable.FindAll_type_keyName(chartype, realname);//注意看，同一个key名在数据库可能不止一个条目。比如一个发波动画定义了两种攻击
                foreach (SkillConfigTable.Row row in toDeleteRows)
                {
                    if (!AllDeletedRecordIDs.Contains(row.RECORD_ID))
                    {
                        Debug.Log("这是一个要删除的ID" + row.RECORD_ID);
                        AllDeletedRecordIDs.Add(row.RECORD_ID);
                    }
                    else
                    {
                        Debug.Log("原SkillConfigTable似乎有重复ID，而且似乎还是因为资源缺失要删除的条目。。");
                    }

                    SkillConfigTable.rowList.Remove(row);
                }
            }
        }

        foreach (SkillConfig newSkillConfig in AllNewSkillConfigsOfAllTypes)
        {
            //if (AllDeletedRecordsIDs.Count > 0)
            //{
            //    newSkillConfig.id = AllDeletedRecordsIDs[0];
            //    Debug.Log(newSkillConfig.keyName + "de 新ID： "+ newSkillConfig.id);
            //    AllDeletedRecordsIDs.RemoveAt(0);
            //    KisoonIDs.Add(newSkillConfig.id);
            //}
            //else{
            //    newSkillConfig.id = MaxOfIntList(KisoonIDs) + 1;
            //    KisoonIDs.Add(newSkillConfig.id);
            //} //没有必要用什么旧ID去补，这个本来工作本来就不能交给自动化，所以上面这些我们给commentout了

            newSkillConfig.RECORD_ID = "plsAddNewIDHere";// RecordId应该开发者自行安排 String.Format("{0:D20}",newid);
            SkillConfigTable.Row newRow = SkillConfigTable.SkillConfigToRow(newSkillConfig);
            if (newRow != null && newRow.REAL_NAME != null)
            {
                SkillConfigTable.rowList.Add(newRow);
            }

            HitBoxLogTable.Row row = new HitBoxLogTable.Row
            {
                RECORD_ID = newSkillConfig.RECORD_ID,
                REAL_NAME = newSkillConfig.REAL_NAME,
                MONSTER_TYPE = newSkillConfig.TYPE,
                Untouched = "0",
                Touched = "0",
                Succeeded = "0"
            };
            HitBoxLogTable.Instance.rowList.Add(row);
        }
        SkillConfigTable.SaveByCurrentRows(Application.dataPath + "/" + path != null ? path : "mst_skill");
        HitBoxLogTable.Instance.SaveByCurrentRows_HitBoxLog(Application.persistentDataPath + "/HitBoxLog.csv", null, null);
    }
}
#endif
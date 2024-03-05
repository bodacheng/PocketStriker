using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;
using System.Linq;
using System.IO;
using System;
using Skill;
using FightScene;

// 这整张代码也就是暂时摆在这提供个“保存技能组”的概念。如果真的以后有需要保存技能组那太多东西需要修。

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        public string AI_States_path; // 我们现在要做的这个游戏完全不牵扯到玩家保存脚本这个事情，但我们自己编辑脚本需要这东西
        public TextAsset usingScript;

        public List<string> PassSkillTypeKeys()//出于初始化的便利而存在的一个函数
        {
            return _statesIncubator?.SkillTypeKeys;
        }

        public void SaveTrans(string chracterType)
        {
            this.SaveStateTransitionInfo(skillEntityList, AI_States_path, chracterType);
        }

        public string ArrangeScriptPathForPlatfom(string PathInStringOrigin)
        {
            string AI_selected = null;
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                AI_selected = PathInStringOrigin;
            }
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsEditor)
            {
                AI_selected = "/Resources/" + PathInStringOrigin + ".xml";
            }
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                AI_selected = PathInStringOrigin;
            }
            return AI_selected;
        }

        public bool SaveStateTransitionInfo(List<SkillEntity> list, string pathAndFileName, string clip_path)
        {
            try
            {
                IDictionary<string, SkillEntity> toFormAttackStateList = new Dictionary<string, SkillEntity>();
                for (int i = 0; i < list.Count; i++)
                {
                    toFormAttackStateList.Add(list[i].REAL_NAME, list[i]);
                }
                _statesIncubator = new BehaviorsIncubator(_emptyState, toFormAttackStateList);
                List<SkillEntity> after_list = new List<SkillEntity>();
                List<string> alreadyInList = new List<string>();
                foreach (SkillEntity s in list)
                {
                    if (!alreadyInList.Contains(s.REAL_NAME) && _statesIncubator.BehaviorDic.Keys.Contains(s.REAL_NAME))
                    {
                        after_list.Add(s);
                        alreadyInList.Add(s.REAL_NAME);
                    }
                }
                
                if (!alreadyInList.Contains("Empty"))
                {
                    SkillEntity Empty = new SkillEntity("Empty", 0, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, 0);
                    after_list.Add(Empty);
                    alreadyInList.Add("Empty");
                }
                
                if (!alreadyInList.Contains("Victory"))
                {
                    SkillEntity Victory =  new SkillEntity("Victory",0,new AIAttrs(), null, null, InputKey.Null, InputKey.Null, 0);
                    after_list.Add(Victory);
                    alreadyInList.Add("Victory");
                }
                
                if (!alreadyInList.Contains("Death"))
                {
                    SkillEntity Death = new SkillEntity("Death", 0, new AIAttrs(), null, null, InputKey.Null, InputKey.Null, 0);
                    after_list.Add(Death);
                    alreadyInList.Add("Death");
                }
                
                if (!alreadyInList.Contains("Hit"))
                {
                    SkillEntity Hit = new SkillEntity("Hit",BehaviorType.Hit, new AIAttrs(),null,null,InputKey.Null, InputKey.Null,0);
                    after_list.Add(Hit);
                    alreadyInList.Add("Hit");
                }
                
                if (!alreadyInList.Contains("KnockOff"))
                {
                    SkillEntity KnockOff = new SkillEntity("KnockOff",  BehaviorType.Hit, new AIAttrs(), null, null, InputKey.Null, InputKey.Null,0);
                    after_list.Add(KnockOff);
                    alreadyInList.Add("KnockOff");
                }
                
                if (!alreadyInList.Contains("getUp"))
                {
                    SkillEntity getUp = new SkillEntity("getUp",  BehaviorType.GetUp, new AIAttrs(), null, null, InputKey.Null, InputKey.Null,0);
                    after_list.Add(getUp);
                    alreadyInList.Add("getUp");
                }
                List<string> DefaultForceToNums = new List<string>() { "Hit", "KnockOff" };

                foreach (SkillEntity s in after_list)
                {
                    List<string> undefined_CausalStateRateSet = new List<string>();
                    foreach (string rs in s.CasualTo)
                    {
                        if (!alreadyInList.Contains(rs))
                        {
                            undefined_CausalStateRateSet.Add(rs);
                        }
                    }
                    List<string> casuals_t = s.CasualTo != null ? s.CasualTo.ToList() : new List<string>();
                    if (undefined_CausalStateRateSet.Any())
                    {
                        foreach (string _set in undefined_CausalStateRateSet)
                        {
                            casuals_t.Remove(_set);// 把连续状态串里出现的没有定义的状态给删除掉。
                        }
                    }
                    s.CasualTo = casuals_t.ToArray();
                    s.ForcedTransitions = s.REAL_NAME != "Death" ? DefaultForceToNums.ToArray() : (new string[0]);
                }
                
                XmlSerializer XmlSerializer = new XmlSerializer(typeof(List<SkillEntity>));
                FileStream FileStream = new FileStream(Application.dataPath + pathAndFileName, FileMode.Create);
                XmlSerializer.Serialize(FileStream, after_list);
                Debug.Log(Application.dataPath + pathAndFileName + " 尝试进行存储");
                FileStream.Close();
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("状态迁移信息保存失败");
                Debug.Log(e.ToString());
                return false;
            }
        }

        public void LoadStatesTransition(TextAsset Script)
        {
            if (Script == null)
            {
                Debug.Log("脚本为空，返回");
                return;
            }
            if (_nowBehavior != null)
            {
                _nowBehavior.AI_State_exit();
            }
            skillEntityList = AIScriptReading.ReadKongfuBook(this, Script); //这个是一个状态清单，生成状态的是States_Dictionary类。
            IDictionary<string, Behavior> Num_State_List = _statesIncubator.BehaviorDic; //理解整个系统的关键
            BehaviourDic = new Dictionary<string, Behavior>();
            foreach (KeyValuePair<string, Behavior> s in Num_State_List)
            {
                BehaviourDic.Add(new KeyValuePair<string, Behavior>(s.Key, s.Value));
            }
            SkillEntityDic = new Dictionary<string, SkillEntity>();
            List<string> alreadyInList = new List<string>();//7.29 这个环节貌似是现在“同技能没法重复”bug的来源
            foreach (SkillEntity _SE in skillEntityList)
            {
                if (_SE.REAL_NAME != null
                    &&
                    !alreadyInList.Contains(_SE.REAL_NAME)
                    &&
                    _statesIncubator.BehaviorDic.Keys.Contains(_SE.REAL_NAME))
                {
                    List<string> new_casual_to = new List<string>();
                    if (_SE.CasualTo == null)
                    {
                        Debug.Log(Script.name + "脚本的" + _SE.REAL_NAME + "状态自然迁移出错,尝试进行强加");
                        _SE.CasualTo = new_casual_to.ToArray();
                    }
                    foreach (string _State_Rate_Set in _SE.CasualTo)
                    {
                        if (!_statesIncubator.BehaviorDic.Keys.Contains(_State_Rate_Set))
                        {
                            Debug.Log(Script.name + "脚本中的状态" + _SE.REAL_NAME + "下存在没有定义的自然迁移状态" + _State_Rate_Set + ",从而已经做强行删除处理。");
                        }
                        else
                        {
                            new_casual_to.Add(_State_Rate_Set);
                        }
                    }
                    SkillEntityDic.Add(new KeyValuePair<string, SkillEntity>(_SE.REAL_NAME,_SE));
                    alreadyInList.Add(_SE.REAL_NAME);
                }
                else
                {
                    if (_SE.REAL_NAME == null)
                    {
                        Debug.Log("脚本中有的状态没有键值");
                    }
                    else
                    {
                        if (!_statesIncubator.BehaviorDic.Keys.Contains(_SE.REAL_NAME))
                        {
                            Debug.Log("脚本中描写的状态的键值:" + _SE.REAL_NAME + " 不存在于我们的定义");
                        }
                    }
                }
            }
        }
        
        public List<SkillEntity> SortStateTransitionSetList(List<SkillEntity> list)
        {
            List<SkillEntity> regularStates = new List<SkillEntity>();
            IDictionary<string, SkillEntity> toFormAttackStateList = new Dictionary<string, SkillEntity>();
            for (int i = 0; i < list.Count; i++)
            {
                toFormAttackStateList.Add(list[i].REAL_NAME, list[i]);
            }
            _statesIncubator = new BehaviorsIncubator(_emptyState,toFormAttackStateList);
            IDictionary<string, SkillEntity> stateTransitionSetDictionary = new Dictionary<string, SkillEntity>();
            List<SkillEntity> setsHaveInitialInput = new List<SkillEntity>();
            
            bool hasD = false, hasR = false;
            
            foreach (SkillEntity _set in list)
            {
                if (_statesIncubator.BehaviorDic.Keys.Contains(_set.REAL_NAME))
                {
                    stateTransitionSetDictionary.Add(new KeyValuePair<string, SkillEntity>(_set.REAL_NAME, _set));
                }
            
                if (_set.EnterInput != InputKey.Null)
                {
                    hasR |= _set.EnterInput == InputKey.Acc;
                    hasD |= _set.EnterInput == InputKey.Defend;
                    setsHaveInitialInput.Add(_set);
                }
            
                if (_set.REAL_NAME == "Hit" || _set.REAL_NAME == "Move")
                {
                    _set.ForcedTransitions = new string[2] { "Hit", "KnockOff" };
                    regularStates.Add(_set);
                }
                if (_set.REAL_NAME == "Empty" || _set.REAL_NAME == "Death" || _set.REAL_NAME == "Victory")
                {
                    _set.ForcedTransitions = new string[0] { };
                    regularStates.Add(_set);
                }
                if (_set.REAL_NAME == "KnockOff")
                {
                    _set.ForcedTransitions = new string[] { "KnockOff" };
                }
            }
            
            List<string> knockOffCasualTrans = new List<string>();
            foreach (SkillEntity _set in setsHaveInitialInput)
            {
                knockOffCasualTrans.Add(_set.REAL_NAME);
            }
            
            foreach (SkillEntity _set in list)
            {
                if (_set.REAL_NAME == "KnockOff")
                {
                    _set.CasualTo = knockOffCasualTrans.ToArray();
                }
            }
            
            SetStateRatesByAILevel(stateTransitionSetDictionary);
            
            List<SkillEntity> allChuans = new List<SkillEntity>();
            foreach (SkillEntity _set in setsHaveInitialInput)
            {
                List<SkillEntity> chuan = new List<SkillEntity>();
                chuan = SearchChuanNext(_set, InputKey.Null, chuan, allChuans, stateTransitionSetDictionary);
            }
            
            foreach (SkillEntity _set in list)
            {
                if (!allChuans.Contains(_set) && !regularStates.Contains(_set) && _set.REAL_NAME != null && _statesIncubator.BehaviorDic.Keys.Contains(_set.REAL_NAME))
                {
                    allChuans.Add(_set);
                }
            }
            regularStates.AddRange(allChuans);
            allChuans.AddRange(regularStates);
            return regularStates;
        }

        List<SkillEntity> SearchChuanNext(SkillEntity _set, InputKey _inputKey, List<SkillEntity> chuan, List<SkillEntity> allChuans, IDictionary<string, SkillEntity> SEDic)
        {
            if (!chuan.Contains(_set) && !allChuans.Contains(_set))
            {
                chuan.Add(_set);
                allChuans.Add(_set);
            }
            
            InputKey searching_inputKey = InputKey.Null;
            searching_inputKey = _inputKey == InputKey.Null ? _set.EnterInput : _inputKey;
            
            foreach (string _rset in _set.CasualTo)
            {
                SkillEntity _SE = SEDic[_rset];
                if (_SE.EnterInput == searching_inputKey && _SE.EnterInput != InputKey.Null)// “chuan”的逻辑其实是说针对有连续输入命令的，自动迁移逻辑不算。并且在这里并不强调一定是同一输入键的攻击串
                {
                    if (!chuan.Contains(_SE) && !allChuans.Contains(_SE))
                    {
                        if (SearchChuanNext(_SE, searching_inputKey, chuan, allChuans, SEDic) == null)
                        {
                            return null;
                        }
                    }
                }
            }
            return chuan;
        }

        bool CheckIfStringInList(string toCheck, List<string> checklist)
        {
            if (checklist == null || toCheck == null)
            {
                return false;
            }
            foreach (string _o in checklist)
            {
                if (toCheck.GetHashCode() == _o.GetHashCode())
                {
                    return true;
                }
            }
            return false;
        }

        List<string> SearchAttackChuanKeyNext(SkillEntity _set, InputKey _inputKey, List<string> chuan, IDictionary<string, SkillEntity> SEDic)
        {
            if (!CheckIfStringInList(_set.REAL_NAME, chuan))
            {
                chuan.Add(_set.REAL_NAME);
            }

            InputKey searching_inputKey = InputKey.Null;
            searching_inputKey = _inputKey == InputKey.Null ? _set.EnterInput : _inputKey;
            foreach (string _rset in _set.CasualTo)
            {
                SkillEntity _SE = SEDic[_rset];
                if (_SE.EnterInput == searching_inputKey && _SE.EnterInput != InputKey.Null)
                {
                    if (!CheckIfStringInList(_SE.REAL_NAME, chuan))
                    {
                        if (SearchAttackChuanKeyNext(_SE, searching_inputKey, chuan, SEDic) == null)
                        {
                            return null;
                        }
                    }
                }
            }
            return chuan;
        }

        public void SetStateRatesByAILevel(IDictionary<string, SkillEntity> SEDic)
        {
            string myMoveStateKey = null;
            //为什么一定要把这些技能串给提前搜出来？事关随着等级提升“解锁技能的处理”。在我们的系统当中技能的首发概率和连段概率是不一样的
            //但无论首发和连段再不一样，我们想把技能解锁这个概念给突出来。如果一个技能没有解锁，那无论是首发也好连段也好这个技能不应该出现。
            //而且在控制模式下也不应该能发出来
            List<string> attackChuan = new List<string>();
            List<string> Fire1Chuan = new List<string>();
            List<string> Fire2Chuan = new List<string>();

            //第一轮循环所应该做的就是把Attack，Fire1，Fire2这三个系列的技能串儿搜出来。
            foreach (KeyValuePair<string, SkillEntity> Transition in SEDic)
            {
                if (Transition.Key == "Move")
                {
                    myMoveStateKey = Transition.Key;
                }

                if (Transition.Value.EnterInput == InputKey.Attack1)
                {
                    attackChuan = SearchAttackChuanKeyNext(Transition.Value, InputKey.Null, attackChuan, SEDic);
                }
                if (Transition.Value.EnterInput == InputKey.Attack2)
                {
                    Fire1Chuan = SearchAttackChuanKeyNext(Transition.Value, InputKey.Null, Fire1Chuan, SEDic);
                }
                if (Transition.Value.EnterInput == InputKey.Attack3)
                {
                    Fire2Chuan = SearchAttackChuanKeyNext(Transition.Value, InputKey.Null, Fire2Chuan, SEDic);
                }
                //在这里把三大攻击串给算出来，无非是说他们的主串包含的技能都有啥名字，这个信息不包括他们各自可能出现的首尾循环
            }

            foreach (KeyValuePair<string, SkillEntity> Transition in SEDic)
            {
                // 三大首发技能AI模式下概率
                if (Transition.Value.EnterInput == InputKey.Attack1)
                {
                    List<string> casuals_now = Transition.Value.CasualTo.ToList();
                    List<string> casual_to_states_after = new List<string>();

                    foreach (string _set in casuals_now)
                    {
                        SkillEntity _SE = SEDic[_set];
                        //接下来这轮分析是整个概率适配系统的关键
                        if (_SE.REAL_NAME != myMoveStateKey)
                        {
                            //然后？概率应该适配多少？
                            if (attackChuan.Contains(_SE.REAL_NAME) || Fire1Chuan.Contains(_SE.REAL_NAME) || Fire2Chuan.Contains(_SE.REAL_NAME))
                            {
                                casual_to_states_after.Add(_set);
                            }
                        }
                    }
                    Transition.Value.CasualTo = casual_to_states_after.ToArray();
                }

                if (Transition.Value.EnterInput == InputKey.Attack2)
                {
                    List<string> casual_to_states_now = Transition.Value.CasualTo.ToList();
                    List<string> casual_to_states_after = new List<string>();

                    foreach (string _set in casual_to_states_now)
                    {
                        SkillEntity _SE = SEDic[_set];
                        //接下来这轮分析是整个概率适配系统的关键
                        if (_SE.REAL_NAME != myMoveStateKey)
                        {
                            //然后？概率应该适配多少？
                            if (attackChuan.Contains(_SE.REAL_NAME) || Fire1Chuan.Contains(_SE.REAL_NAME) || Fire2Chuan.Contains(_SE.REAL_NAME))
                            {
                                casual_to_states_after.Add(_set);
                            }
                        }
                    }
                    Transition.Value.CasualTo = casual_to_states_after.ToArray();
                }

                if (Transition.Value.EnterInput == InputKey.Attack3)
                {
                    List<string> casual_to_states_now = Transition.Value.CasualTo.ToList();
                    List<string> casual_to_states_after = new List<string>();
                    
                    foreach (string _set in casual_to_states_now)
                    {
                        SkillEntity _SE = SEDic[_set];
                        //接下来这轮分析是整个概率适配系统的关键
                        if (_SE.REAL_NAME != myMoveStateKey)
                        {
                            //然后？概率应该适配多少？
                            if (attackChuan.Contains(_SE.REAL_NAME) || Fire1Chuan.Contains(_SE.REAL_NAME) || Fire2Chuan.Contains(_SE.REAL_NAME))
                            {
                                casual_to_states_after.Add(_set);
                            }
                        }
                    }
                    Transition.Value.CasualTo = casual_to_states_after.ToArray();
                }

                //非首发
                if ((Transition.Value.EnterInput != InputKey.Attack3 &&
                    Transition.Value.EnterInput != InputKey.Attack2 && 
                    Transition.Value.EnterInput != InputKey.Attack1) &&
                    (attackChuan.Contains(Transition.Value.REAL_NAME) || Fire1Chuan.Contains(Transition.Value.REAL_NAME) || Fire2Chuan.Contains(Transition.Value.REAL_NAME)))
                {
                    List<string> casual_to_states_now = Transition.Value.CasualTo.ToList();
                    List<string> casual_to_states_after = new List<string>();

                    foreach (string _set in casual_to_states_now)
                    {
                        SkillEntity _SE = SEDic[_set];
                        if (_SE.REAL_NAME != myMoveStateKey && (attackChuan.Contains(_SE.REAL_NAME) || Fire1Chuan.Contains(_SE.REAL_NAME) || Fire2Chuan.Contains(_SE.REAL_NAME)))
                        {
                            casual_to_states_after.Add(_set);
                        }
                    }
                    Transition.Value.CasualTo = casual_to_states_after.ToArray();
                }

                // 移动概率
                if (Transition.Key == "Move")
                {
                    Transition.Value.CasualTo = new string[] { };
                }
            }
        }
    }
}
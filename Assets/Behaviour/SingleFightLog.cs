using System.Collections.Generic;
using Skill;
using UnityEngine;

namespace Soul
{
    public class SingleFightLog
    {
        readonly List<FightRecord> MyBehaviourHistory = new List<FightRecord>();
        public IDictionary<string, int> StateTriggerdTimes = new Dictionary<string, int>();
        public readonly IDictionary<string, int> StateInterruptedTimes = new Dictionary<string, int>();
        
        public void WriteLog(FightRecord behaviourHistory)
        {
            MyBehaviourHistory.Add(behaviourHistory);
        }
        
        public class FightRecord
        {
            public string stateKey;
            public BehaviorType behaviorType;
        }
        
        public class BehaviourFightRecord : FightRecord
        {
            public bool AI_Decided;
            public string whyIDidThis;
        }
        
        public class PositiveRecord : FightRecord
        {
        }
        public class NegativeRecord : FightRecord
        {
        }

        // 这个函数待写。我们设想可以每次战斗结束后进行个总的报告，来分析各个技能有没有取得正面效应。
        public void Summary()
        {
            for (int i = 0; i < MyBehaviourHistory.Count; i++)
            {
                if (MyBehaviourHistory[i] is BehaviourFightRecord)
                {
                    if (StateTriggerdTimes.ContainsKey(MyBehaviourHistory[i].stateKey))
                    {
                        StateTriggerdTimes[MyBehaviourHistory[i].stateKey] += 1;
                    }
                    else
                    {
                        StateTriggerdTimes.Add(MyBehaviourHistory[i].stateKey, 1);
                    }
                    
                    if (i != MyBehaviourHistory.Count - 1)
                    {
                        if (MyBehaviourHistory[i+1] is NegativeRecord)
                        {
                            if (StateInterruptedTimes.ContainsKey(MyBehaviourHistory[i].stateKey))
                            {
                                StateInterruptedTimes[MyBehaviourHistory[i].stateKey] += 1;
                            }
                            else
                            {
                                StateInterruptedTimes.Add(MyBehaviourHistory[i].stateKey, 1);
                            }
                        }
                    }
                }
            }
        }
        
        public void Clear()
        {
            MyBehaviourHistory.Clear();
            StateTriggerdTimes.Clear();
            StateInterruptedTimes.Clear();
        }

        readonly MultiDic<string, string, int> skillNoBenefitLog = new MultiDic<string, string, int>();
        // 本函数的运行紧邻WriteLog之后
        public void AnalysisLog(MultiDic<string, string, int> _ConditionAndRespondPriority)
        {
            if (MyBehaviourHistory.Count % 10 != 0 || MyBehaviourHistory.Count < 10)
            {
                return;
            }
            skillNoBenefitLog.Clear();
            for (int i = MyBehaviourHistory.Count - 10 + 2; i < MyBehaviourHistory.Count; i++)
            {
                FightRecord fightRecord = MyBehaviourHistory[i];
                if (fightRecord is BehaviourFightRecord)
                {
                    if ((MyBehaviourHistory[i - 2] is BehaviourFightRecord) && (MyBehaviourHistory[i - 1] is BehaviourFightRecord))// 发招两次没有获得正面效益
                    {
                        BehaviourFightRecord behaviourFightRecord = (BehaviourFightRecord)MyBehaviourHistory[i - 2];
                        if (behaviourFightRecord.AI_Decided)
                        {
                            skillNoBenefitLog.Set(behaviourFightRecord.whyIDidThis, MyBehaviourHistory[i - 2].stateKey,
                            skillNoBenefitLog.Get(behaviourFightRecord.whyIDidThis, MyBehaviourHistory[i - 2].stateKey, 0) + 1);
                        }
                    }
                }
                if (fightRecord is NegativeRecord)
                {
                    if (MyBehaviourHistory[i - 1] is BehaviourFightRecord)
                    {
                        BehaviourFightRecord behaviourFightRecord = (BehaviourFightRecord)MyBehaviourHistory[i - 1];
                        if (behaviourFightRecord.AI_Decided)
                        {
                            skillNoBenefitLog.Set(behaviourFightRecord.whyIDidThis, MyBehaviourHistory[i - 1].stateKey,
                            skillNoBenefitLog.Get(behaviourFightRecord.whyIDidThis, MyBehaviourHistory[i - 1].stateKey, 0) + 1);
                        }
                    }
                }
            }
            
            if (skillNoBenefitLog.GetValues().Count > 0)
            {
                foreach(KeyValuePair<(string, string) ,int> keyValuePair in skillNoBenefitLog.mDict)
                {
                    if (skillNoBenefitLog.Get(keyValuePair.Key.Item1, keyValuePair.Key.Item2) > 2) // 10个连续状态记录里某个条件反应大于两次没有正面效应，说明这个因果关系没有好的效益，修改其优先度
                    {
                        _ConditionAndRespondPriority.Set(keyValuePair.Key.Item1, keyValuePair.Key.Item2, keyValuePair.Value + 1);
                    }
                }
            }
        }
    }
}
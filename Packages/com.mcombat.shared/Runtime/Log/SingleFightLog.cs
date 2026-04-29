using System.Collections.Generic;
using Skill;

namespace Soul
{
    public class SingleFightLog
    {
        readonly List<FightRecord> _behaviourHistory = new List<FightRecord>();
        readonly MultiDic<string, string, int> _skillNoBenefitLog = new MultiDic<string, string, int>();

        public IDictionary<string, int> StateTriggerdTimes = new Dictionary<string, int>();
        public readonly IDictionary<string, int> StateInterruptedTimes = new Dictionary<string, int>();

        public void WriteLog(FightRecord behaviourHistory)
        {
            _behaviourHistory.Add(behaviourHistory);
        }

        public void Summary()
        {
            for (var i = 0; i < _behaviourHistory.Count; i++)
            {
                if (_behaviourHistory[i] is not BehaviourFightRecord)
                {
                    continue;
                }

                var aggregateKey = AggregateKey(_behaviourHistory[i]);
                if (string.IsNullOrEmpty(aggregateKey))
                {
                    continue;
                }

                Increment(StateTriggerdTimes, aggregateKey);
                if (i != _behaviourHistory.Count - 1 && _behaviourHistory[i + 1] is NegativeRecord)
                {
                    Increment(StateInterruptedTimes, aggregateKey);
                }
            }
        }

        public void Clear()
        {
            _behaviourHistory.Clear();
            StateTriggerdTimes.Clear();
            StateInterruptedTimes.Clear();
        }

        public void AnalysisLog(MultiDic<string, string, int> conditionAndRespondPriority)
        {
            if (_behaviourHistory.Count % 10 != 0 || _behaviourHistory.Count < 10)
            {
                return;
            }

            _skillNoBenefitLog.Clear();
            for (var i = _behaviourHistory.Count - 10 + 2; i < _behaviourHistory.Count; i++)
            {
                var fightRecord = _behaviourHistory[i];
                if (fightRecord is BehaviourFightRecord)
                {
                    if (_behaviourHistory[i - 2] is BehaviourFightRecord && _behaviourHistory[i - 1] is BehaviourFightRecord)
                    {
                        AddNoBenefitLog(_behaviourHistory[i - 2]);
                    }
                }

                if (fightRecord is NegativeRecord)
                {
                    AddNoBenefitLog(_behaviourHistory[i - 1]);
                }
            }

            if (_skillNoBenefitLog.GetValues().Count <= 0)
            {
                return;
            }

            foreach (var keyValuePair in _skillNoBenefitLog.mDict)
            {
                if (_skillNoBenefitLog.Get(keyValuePair.Key.Item1, keyValuePair.Key.Item2) > 2)
                {
                    conditionAndRespondPriority.Set(keyValuePair.Key.Item1, keyValuePair.Key.Item2, keyValuePair.Value + 1);
                }
            }
        }

        static string AggregateKey(FightRecord fightRecord)
        {
            return fightRecord == null
                ? null
                : string.IsNullOrEmpty(fightRecord.skillId) ? fightRecord.stateKey : fightRecord.skillId;
        }

        static void Increment(IDictionary<string, int> counter, string key)
        {
            if (counter.ContainsKey(key))
            {
                counter[key] += 1;
            }
            else
            {
                counter.Add(key, 1);
            }
        }

        void AddNoBenefitLog(FightRecord fightRecord)
        {
            if (fightRecord is not BehaviourFightRecord behaviourFightRecord || !behaviourFightRecord.AI_Decided)
            {
                return;
            }

            _skillNoBenefitLog.Set(
                behaviourFightRecord.whyIDidThis,
                fightRecord.stateKey,
                _skillNoBenefitLog.Get(behaviourFightRecord.whyIDidThis, fightRecord.stateKey, 0) + 1);
        }

        public class FightRecord
        {
            public string skillId;
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
    }
}

using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Log
{
    public class HitBoxLogger
    {
        static HitBoxLogger instance;
        public static HitBoxLogger Instance
        {
            get
            {
                if (instance == null)
                    instance = new HitBoxLogger();
                return instance;
            }
        }

        public List<KeyValuePair<string, HitBoxLifeEnding>> HitBoxersEndings = new List<KeyValuePair<string, HitBoxLifeEnding>>();
        public IDictionary<string, int> untouchedtimes = new Dictionary<string, int>();
        public IDictionary<string, int> touchedtimes = new Dictionary<string, int>();
        public IDictionary<string, int> successedtimes = new Dictionary<string, int>();

        public void AddLog(string stakeKey, HitBoxLifeEnding hitBoxLifeEnding)
        {
            HitBoxersEndings.Add(new KeyValuePair<string, HitBoxLifeEnding>(stakeKey, hitBoxLifeEnding));
        }

        public void Clear()
        {
            HitBoxersEndings.Clear();
            untouchedtimes.Clear();
            touchedtimes.Clear();
            successedtimes.Clear();
        }

        // 获取现版本text
        public string LoadCurrentToString()
        {
            if (File.Exists(Application.persistentDataPath + "/" + CommonSetting.SkillDynamicAnalysis))
            {
                string level = File.ReadAllText(Application.persistentDataPath + "/" + CommonSetting.SkillDynamicAnalysis);
                return level;
            }
            else
            {
                HitBoxLogTable.Instance.rowList = new List<HitBoxLogTable.Row>();
                for (int i = 0; i < SkillConfigTable.rowList.Count; i++)
                {
                    if (string.IsNullOrEmpty(SkillConfigTable.rowList[i].RECORD_ID))
                        continue;
                    HitBoxLogTable.Row row = new HitBoxLogTable.Row
                    {
                        RECORD_ID = SkillConfigTable.rowList[i].RECORD_ID,
                        REAL_NAME = SkillConfigTable.rowList[i].REAL_NAME,
                        MONSTER_TYPE = SkillConfigTable.rowList[i].TYPE,
                        Untouched = "0",
                        Touched = "0",
                        Succeeded = "0",
                        TriggeredTimes = "0",
                        InterruptedTimes = "0"
                    };
                    HitBoxLogTable.Instance.rowList.Add(row);
                }
                HitBoxLogTable.Instance.SaveByCurrentRows_HitBoxLog(Application.persistentDataPath + "/" + CommonSetting.SkillDynamicAnalysis, null, null);
                string level = File.ReadAllText(Application.persistentDataPath + "/" + CommonSetting.SkillDynamicAnalysis);
                return level;
            }
        }

        public void LogSummit()
        {
            for (int i = 0; i < HitBoxersEndings.Count; i++)
            {
                if (HitBoxersEndings[i].Key == null)
                    continue;
                switch (HitBoxersEndings[i].Value)
                {
                    case HitBoxLifeEnding.untouched:
                        if (untouchedtimes.ContainsKey(HitBoxersEndings[i].Key))
                            untouchedtimes[HitBoxersEndings[i].Key] += 1;
                        else
                            untouchedtimes.Add(HitBoxersEndings[i].Key, 1);
                        break;
                    case HitBoxLifeEnding.touched:
                        if (touchedtimes.ContainsKey(HitBoxersEndings[i].Key))
                            touchedtimes[HitBoxersEndings[i].Key] += 1;
                        else
                            touchedtimes.Add(HitBoxersEndings[i].Key, 1);
                        break;
                    case HitBoxLifeEnding.successed:
                        if (successedtimes.ContainsKey(HitBoxersEndings[i].Key))
                            successedtimes[HitBoxersEndings[i].Key] += 1;
                        else
                            successedtimes.Add(HitBoxersEndings[i].Key, 1);
                        break;
                }
            }
        }
    }

    public enum HitBoxLifeEnding
    {
        untouched = 1,
        touched = 2,
        successed = 3
    }
}
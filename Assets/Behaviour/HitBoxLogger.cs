using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Log
{
    public class HitBoxLogger : HitBoxLoggerCore
    {
        static HitBoxLogger instance;

        public static HitBoxLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new HitBoxLogger();
                }

                return instance;
            }
        }

        public string LoadCurrentToString()
        {
            var path = Application.persistentDataPath + "/" + CommonSetting.SkillDynamicAnalysis;
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            HitBoxLogTable.Instance.rowList = new List<HitBoxLogTable.Row>();
            for (var i = 0; i < SkillConfigTable.rowList.Count; i++)
            {
                if (string.IsNullOrEmpty(SkillConfigTable.rowList[i].RECORD_ID))
                {
                    continue;
                }

                HitBoxLogTable.Instance.rowList.Add(new HitBoxLogTable.Row
                {
                    RECORD_ID = SkillConfigTable.rowList[i].RECORD_ID,
                    REAL_NAME = SkillConfigTable.rowList[i].REAL_NAME,
                    MONSTER_TYPE = SkillConfigTable.rowList[i].TYPE,
                    Untouched = "0",
                    Touched = "0",
                    Succeeded = "0",
                    TriggeredTimes = "0",
                    InterruptedTimes = "0"
                });
            }

            HitBoxLogTable.Instance.SaveByCurrentRows_HitBoxLog(path, null, null);
            return File.ReadAllText(path);
        }
    }
}

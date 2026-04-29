using System;
using System.Collections.Generic;
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
                {
                    instance = new HitBoxLogger();
                }

                return instance;
            }
        }

        public readonly List<KeyValuePair<string, HitBoxLifeEnding>> HitBoxersEndings = new List<KeyValuePair<string, HitBoxLifeEnding>>();
        public readonly IDictionary<string, int> untouchedtimes = new Dictionary<string, int>();
        public readonly IDictionary<string, int> touchedtimes = new Dictionary<string, int>();
        public readonly IDictionary<string, int> successedtimes = new Dictionary<string, int>();

        public void AddLog(string stakeKey, HitBoxLifeEnding hitBoxLifeEnding)
        {
            HitBoxersEndings.Add(new KeyValuePair<string, HitBoxLifeEnding>(stakeKey, hitBoxLifeEnding));
        }

        public static string LoadOrCreateLogFile(string path, Action createFile)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            createFile?.Invoke();
            return File.ReadAllText(path);
        }

        public void Clear()
        {
            HitBoxersEndings.Clear();
            untouchedtimes.Clear();
            touchedtimes.Clear();
            successedtimes.Clear();
        }

        public void LogSummit()
        {
            for (var i = 0; i < HitBoxersEndings.Count; i++)
            {
                var ending = HitBoxersEndings[i];
                if (ending.Key == null)
                {
                    continue;
                }

                switch (ending.Value)
                {
                    case HitBoxLifeEnding.untouched:
                        Increment(untouchedtimes, ending.Key);
                        break;
                    case HitBoxLifeEnding.touched:
                        Increment(touchedtimes, ending.Key);
                        break;
                    case HitBoxLifeEnding.successed:
                        Increment(successedtimes, ending.Key);
                        break;
                }
            }
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
    }
}

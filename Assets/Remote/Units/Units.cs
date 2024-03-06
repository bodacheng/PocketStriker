using System.Collections.Generic;

namespace dataAccess
{
    public static class Units
    {
        public static readonly IDictionary<string, UnitInfo> Dic = new Dictionary<string, UnitInfo>();
        
        public static bool CheckExist(string key)
        {
            if (key == null)
            {
                return false;
            }
            if (Dic.ContainsKey(key))
            {
                if (Dic[key] != null)
                    return true;
            }
            return false;
        }
        
        public static UnitInfo Get(string instanceId)
        {
            if (instanceId == null)
            {
                return null;
            }
            if (Dic.ContainsKey(instanceId))
            {
                if (Dic[instanceId] != null)
                    return Dic[instanceId];
            }
            return null;
        }
        
        public static UnitInfo GetByRId(string r_id)
        {
            foreach (var kv in Dic)
            {
                if (kv.Value.r_id == r_id)
                {
                    return kv.Value;
                }
            }
            return null;
        }
    }
}
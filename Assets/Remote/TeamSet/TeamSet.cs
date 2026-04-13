using UnityEngine;

namespace dataAccess
{
    public static partial class TeamSet
    {
        public static PosKeySet Default = new PosKeySet();
        public static PosKeySet Origin = new PosKeySet();
        public static PosKeySet Arena3V3 = new PosKeySet();
        public static PosKeySet Gangbang = new PosKeySet();

        public static PosKeySet GetTargetSet(string mode)
        {
            switch(mode)
            {
                case "arcade":
                    return Default;
                case "arena":
                    return Arena3V3;
                case "gangbang":
                    return Gangbang;
                case "origin":
                    return Origin;
            }
            return null;
        }
        
        public static PosKeySet DicToPosKeySet(MultiDic<int, int, UnitInfo> dic)
        {
            var posKeySet = new PosKeySet();
            foreach (var kv in dic.mDict)
            {
                posKeySet.SetPosMemInfoByInstanceID(kv.Key.Item2, kv.Value.id);
            }
            return posKeySet;
        }

        public static void SanitizeAgainstCurrentInventory(string fallbackInstanceId = null)
        {
            Default = SanitizeSet(Default, "arcade", fallbackInstanceId);
            Arena3V3 = SanitizeSet(Arena3V3, "arena", fallbackInstanceId);
            Gangbang = SanitizeSet(Gangbang, "gangbang", fallbackInstanceId);
            Origin = SanitizeSet(Origin, "origin", fallbackInstanceId);
        }

        static PosKeySet SanitizeSet(PosKeySet targetSet, string setName, string fallbackInstanceId)
        {
            targetSet ??= new PosKeySet();
            var hasValidMember = false;
            foreach (var oneSet in targetSet.PosNumsWithLocalKeys)
            {
                if (string.IsNullOrEmpty(oneSet.instanceID))
                {
                    continue;
                }

                if (Units.Get(oneSet.instanceID) == null)
                {
                    Debug.LogWarning($"[TeamCompat] Clear unsupported team member {oneSet.instanceID} from {setName}:{oneSet.posNum}");
                    oneSet.instanceID = null;
                    continue;
                }

                hasValidMember = true;
            }

            if (!hasValidMember && !string.IsNullOrEmpty(fallbackInstanceId) && Units.Get(fallbackInstanceId) != null)
            {
                targetSet.SetPosMemInfoByInstanceID(0, fallbackInstanceId);
            }
            return targetSet;
        }
    }
}

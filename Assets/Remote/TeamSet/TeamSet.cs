namespace dataAccess
{
    public static partial class TeamSet
    {
        public static PosKeySet Default = new PosKeySet();
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
    }
}

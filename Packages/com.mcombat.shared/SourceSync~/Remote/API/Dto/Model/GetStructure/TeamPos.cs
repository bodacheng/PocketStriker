using System;
using System.Collections.Generic;

namespace dataAccess
{
    /// <summary>
    /// プレーヤ所有出戦チームモデル
    /// 作成者：Auto Generated
    /// バージョン：1.00 2019/07/02
    /// </summary>
    [Serializable]
    public class TeamPos
    {
        /// <summary>
        /// プレーヤ所有モンスターID(前)
        /// </summary>
        public string f { get; set; }

        /// <summary>
        /// プレーヤ所有モンスターID(左)
        /// </summary>
        public string l { get; set; }

        /// <summary>
        /// プレーヤ所有モンスターID(右)
        /// </summary>
        public string r { get; set; }

        public PosKeySet ToPosKeySet()
        {
            var PosKeySet = new PosKeySet();
            var posNumWithLocalKeys = new List<PosKeySet.OneSet>
            {
                new PosKeySet.OneSet(0, f),
                new PosKeySet.OneSet(1, l),
                new PosKeySet.OneSet(2, r),
            };
            PosKeySet.PosNumsWithLocalKeys = posNumWithLocalKeys.ToArray();
            return PosKeySet;
        }
    }
}

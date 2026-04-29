using System;

namespace Api.Dto.Model.Common {

    /// <summary>
    /// 抽象的モデル
    /// </summary>
    [Serializable]
    public class AbstractModel<T> {

        /// <summary>
        /// HTTPステータス
        /// </summary>
        public int httpStatus { get; set; }

        /// <summary>
        /// ステータス
        /// </summary>
        public int status { get; set; }

        /// <summary>
        /// メッセージ
        /// </summary>
        public string message { get; set; }

        /// <summary>
        /// 詳細メッセージ
        /// </summary>
        public string detailMessage { get; set; }

        /// <summary>
        /// データ
        /// </summary>
        public T data { get; set; }
    }
}
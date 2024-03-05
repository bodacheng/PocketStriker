using System;
using System.IO;
using System.Collections.Generic;

namespace Cocone.ProjectP3
{
    /// <summary>
    /// ビルド構成を設定
    /// </summary>
    public class BuildConfiguration
    {
        /// <summary>
        /// ApplicaionID e.g. com.cocone...
        /// </summary>
        public string applicationIdentifier { get; private set; }
        
        /// <summary>
        /// プロダクト名
        /// </summary>
        public string productName { get; private set; }
        
        /// <summary>
        /// info.plistのCFBundleName
        /// </summary>
        public string cfBundleName { get; private set; }
        
        /// <summary>
        /// info.plistのCFBundleExecutable値
        /// </summary>
        public string cfBundleExecutableName { get; private set; }
        
        /// <summary>
        /// 端末でのアプリ表示名
        /// </summary>
        public string appDisplayName { get; private set; }
        
        /// <summary>
        /// keystoreの配置場所
        /// </summary>
        public string keystoreName { get; private set; }
        
        /// <summary>
        /// keyalias名
        /// </summary>
        public string keyaliasName { get; private set; }

        /// <summary>
        /// 追加で付与したいDefine Symbols
        /// </summary>
        public HashSet<string> addDefineSymbols { get; private set; }

        /// <summary>
        /// アセットをアップロードするs3://のバケットアドレス
        /// </summary>
        public string assetUploadAddress { get; private set; }

        /// <summary>
        /// Vivox settings
        /// </summary>
        public string vivoxServer { get; private set; }
        public string vivoxDomain { get; private set; }
        public string vivoxTokenIssuer { get; private set; }
        public string vivoxTokenKey { get; private set; }
        
        /// <summary>
        /// ファイルの読み込み
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static BuildConfiguration Deserialize(string path)
        {
            return YamlDeserializer.Deserialize<BuildConfiguration>(path);
        }
    }
}

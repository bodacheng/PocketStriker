using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions.Must;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cocone.ProjectP3
{
    /// <summary>
    /// Yamlの読み込み
    /// </summary>
    public class YamlDeserializer
    { 
        public static T Deserialize<T>(string yamlPath)
        {
            // テキスト抽出
            var input = new StreamReader(yamlPath, Encoding.UTF8);

            // デシリアライザインスタンス作成
            var deserializer = new Deserializer();

            try
            {
                // yamlデータのオブジェクトを作成
                var deserializeObject = deserializer.Deserialize<T>(input);
				return deserializeObject;
            }
            catch (Exception e)
            {
                Debug.Log($"ファイルが読み込みでエラーが発生しました。Path = {yamlPath}");
                throw new Exception(e.Message);
            }
        }
    }    
}

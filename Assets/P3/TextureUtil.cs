using System;
using System.IO;
using UnityEngine;

namespace ModelView
{
    public static class TextureUtil
    {
        public static Texture2D ToTexture2D(Texture self, int textureSize)
        {
            var format = TextureFormat.RGBA32;
            var result = new Texture2D(textureSize, textureSize, format, false);
            var currentRT = RenderTexture.active;
            var rt = new RenderTexture(textureSize, textureSize, 32);
            Graphics.Blit(self, rt);
            RenderTexture.active = rt;
            var source = new Rect(0, 0, rt.width, rt.height);
            result.ReadPixels(source, 0, 0);
            result.Apply();
            RenderTexture.active = currentRT;
            return result;
        }
        
        public static Texture2D ToTexture2D(Texture self)
        {
            var format = TextureFormat.RGBA32;
            var result = new Texture2D(self.width, self.height, format, false);
            var currentRT = RenderTexture.active;
            var rt = new RenderTexture(self.width, self.height, 32);
            Graphics.Blit(self, rt);
            RenderTexture.active = rt;
            var source = new Rect(0, 0, rt.width, rt.height);
            result.ReadPixels(source, 0, 0);
            result.Apply();
            RenderTexture.active = currentRT;
            return result;
        }
        
        public static void SavePng(Texture2D texture2D, string path, string filename)
        {
            try
            {
                //Write to a file in the project folder
                if (!Directory.Exists(path))
                {
                    Debug.Log("path created:" + path);
                    Directory.CreateDirectory(path);
                }
                
                var filePath = path + "/" + filename;
                var png = texture2D.EncodeToPNG();
                File.WriteAllBytes(filePath, png);
                Debug.Log("png file output success:" + filePath);
                // AssetDatabase.Refresh();
                // AssetDatabase.ImportAsset(filePath);
                // TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
                // importer.textureType= TextureImporterType.Sprite;
                // AssetDatabase.WriteImportSettingsIfDirty(filePath);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
    }
}

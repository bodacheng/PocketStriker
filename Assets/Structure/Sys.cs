using UnityEngine;
using System.Linq;
using System.IO;

public class Sys
{
    // 将某文件夹下指定后缀文件复制到目标文件夹。不同平台下由于文件被压缩方式不同可能无法找到目标文件
    void CopyFileTo(string sourceDir, string backupDir, string extension)
    {
        if (!Directory.Exists(backupDir))
        {
            //if it doesn't, create it
            Directory.CreateDirectory(backupDir);
        }
        string[] picList;
        if (extension != null)
            picList = Directory.GetFiles(sourceDir).Where(file => file.ToLower().EndsWith(extension)).ToArray();
        else
            picList = Directory.GetFiles(sourceDir);

        Debug.Log(sourceDir + "下找到" + picList.Length + "个json文件");

        foreach (string f in picList)
        {
            // Remove path from the file name.
            string fName = f.Substring(sourceDir.Length + 1);

            // Use the Path.Combine method to safely append the file name to the path.
            // Will overwrite if the destination file already exists.
            File.Copy(Path.Combine(sourceDir, fName), Path.Combine(backupDir, fName), true);
        }
    }
}

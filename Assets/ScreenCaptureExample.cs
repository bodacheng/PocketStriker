using System.IO;
using UnityEngine;

public class ScreenCaptureExample : MonoBehaviour
{
    // 按键设置，你可以自行修改
    public KeyCode captureKey = KeyCode.Space;
    // 保存图片的路径
    public string screenshotFolderPath = "Screenshots";

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            CaptureScreenshot();
        }
    }
    
#endif
    
    void CaptureScreenshot()
    {
        // 确保文件夹存在
        if (!Directory.Exists(screenshotFolderPath))
        {
            Directory.CreateDirectory(screenshotFolderPath);
        }

        // 使用时间戳创建文件名，以免重复
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = "Screenshot_" + timestamp + ".png";
        string fullPath = Path.Combine(screenshotFolderPath, filename);

        // 捕捉并保存截图
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log("Screenshot saved at: " + fullPath);
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class DALLEService
{
    private readonly OpenAIConfig config;
    
    public DALLEService(OpenAIConfig config)
    {
        this.config = config;
    }
    
    [Serializable] class DALLERequest { public string model; public string prompt; public int n = 1; }
    [Serializable] class DALLEData { public string url; public string revised_prompt; }
    [Serializable] class DALLEResponse { public DALLEData[] data; }

    public async System.Threading.Tasks.Task<Texture2D[]> GenerateImagesDALLEAsync(string prompt, int count = 1, string size = "1024x1024", int? timeoutMs = null)
    {
        var actualTimeout = timeoutMs ?? config.ImageTimeoutMs;
        var actualCount = Mathf.Clamp(count, 1, 4);
        
        // 将所有参数作为prompt的一部分，确保模型兼容性
        var enhancedPrompt = $"{prompt}, {config.DefaultStyle} style, {size} aspect ratio, {config.DefaultQuality} quality";
        
        var body = new DALLERequest {
            model = config.ImageModel,
            prompt = enhancedPrompt,
            n = actualCount
        };
        var json = JsonUtility.ToJson(body);
        var url = "https://api.openai.com/v1/images/generations";

        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");
        req.timeout = Mathf.CeilToInt(actualTimeout / 1000f);

        var tcs = new System.Threading.Tasks.TaskCompletionSource<Texture2D[]>();
        MonoBehaviourRunner.Instance.StartCoroutine(SendDALLE(req, tcs));
        return await tcs.Task;
    }

    private IEnumerator SendDALLE(UnityWebRequest req, System.Threading.Tasks.TaskCompletionSource<Texture2D[]> tcs)
    {
        yield return req.SendWebRequest();
    #if UNITY_2020_2_OR_NEWER
        bool hasError = req.result != UnityWebRequest.Result.Success;
    #else
        bool hasError = req.isHttpError || req.isNetworkError;
    #endif
        if (hasError) { 
            tcs.SetException(new Exception($"HTTP {(int)req.responseCode}: {req.error}\n{req.downloadHandler?.text}")); 
            yield break; 
        }

        var text = req.downloadHandler.text;
        DALLEResponse resp;
        try
        {
            resp = JsonUtility.FromJson<DALLEResponse>(text);
            if (resp?.data == null || resp.data.Length == 0) 
                throw new Exception("No images generated.\nRaw: " + text);
        }
        catch (Exception e) { 
            tcs.SetException(new Exception("Parse error: " + e.Message + "\nRaw: " + text)); 
            yield break;
        }

        var list = new List<Texture2D>();
        foreach (var data in resp.data)
        {
            var imageReq = new UnityWebRequest(data.url);
            imageReq.downloadHandler = new DownloadHandlerBuffer();
            yield return imageReq.SendWebRequest();
            
            if (imageReq.result == UnityWebRequest.Result.Success)
            {
                var bytes = imageReq.downloadHandler.data;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes)) list.Add(tex);
            }
        }
        if (list.Count == 0) 
        {
            tcs.SetException(new Exception("All images failed to load."));
        }
        else
        {
            tcs.SetResult(list.ToArray());
        }
    }
}

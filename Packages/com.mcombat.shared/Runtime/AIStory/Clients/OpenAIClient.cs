using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIClient : IAIClient
{
    private readonly OpenAIConfig config;
    private readonly DALLEService dalleService;
    public DALLEService DALLEService => dalleService;
    
    private const string BaseUrl = "https://api.openai.com/v1/chat/completions";
    
    public string ProviderName => "OpenAI";
    public bool IsConfigured => config != null && config.IsValid();
    
    public OpenAIClient(OpenAIConfig config)
    {
        this.config = config;
        this.dalleService = new DALLEService(config);
        Debug.Log("OpenAIClient Generated:" + this.config);
    }
    
    [Serializable] class Message { public string role; public string content; }
    [Serializable] class RequestBody { public string model; public Message[] messages; public int max_completion_tokens = 1000; }
    [Serializable] class Choice { public Message message; }
    [Serializable] class ResponseRoot { public Choice[] choices; }
    
    /// <summary>
    /// Send a "question" and return OpenAI's text response
    /// </summary>
    public System.Threading.Tasks.Task<string> AskAsync(string question, int? timeoutMs = null)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        var actualTimeout = timeoutMs ?? config.DefaultTimeoutMs;

        // Construct request body
        var body = new RequestBody {
            model = config.TextModel,
            messages = new [] {
                new Message { role = "user", content = question }
            }
        };
        var json = JsonUtility.ToJson(body);
        
        var req = new UnityWebRequest(BaseUrl, "POST");
        byte[] raw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");
        req.timeout = Mathf.CeilToInt(actualTimeout / 1000f);

        // Use MonoBehaviourRunner to execute coroutine
        MonoBehaviourRunner.Instance.StartCoroutine(Send(req, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator Send(UnityWebRequest req, System.Threading.Tasks.TaskCompletionSource<string> tcs)
    {
        yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
        bool hasError = req.result != UnityWebRequest.Result.Success;
#else
        bool hasError = req.isHttpError || req.isNetworkError;
#endif
        if (hasError)
        {
            tcs.SetException(new Exception($"HTTP {(int)req.responseCode}: {req.error}\n{req.downloadHandler?.text}"));
            yield break;
        }

        var text = req.downloadHandler.text;
        try
        {
            var resp = JsonUtility.FromJson<ResponseRoot>(text);
            string answer = "(empty)";
            if (resp?.choices != null && resp.choices.Length > 0 &&
                resp.choices[0].message?.content != null)
            {
                answer = resp.choices[0].message.content;
            }
            tcs.SetResult(answer);
        }
        catch (Exception e)
        {
            tcs.SetException(new Exception("Parse error: " + e.Message + "\nRaw: " + text));
        }
    }
    
    public async System.Threading.Tasks.Task<Texture2D[]> GeneratePic(string prompt, int? count = null, string aspectRatio = null)
    {
        var actualCount = count ?? config.DefaultImageCount;
        var actualSize = aspectRatio ?? config.DefaultSize;
        
        var data = await dalleService.GenerateImagesDALLEAsync(prompt, actualCount, actualSize);
        return data;
    }
}

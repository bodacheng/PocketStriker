using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class Imagen4Service
{
    private readonly GeminiConfig config;
    private readonly Func<string, object, Task<CloudScriptImageResponse>> executeFunction;
    
    public Imagen4Service(GeminiConfig config, Func<string, object, Task<CloudScriptImageResponse>> executeFunction)
    {
        this.config = config;
        this.executeFunction = executeFunction;
    }
    
    [Serializable] public class ImagenPrediction { public string bytesBase64Encoded; public string mimeType; public string prompt; }
    [Serializable] public class ImageInfo { public string url; public string mimeType; }
    [Serializable] public class CloudScriptImageResponse
    {
        public ImageInfo[] images;
        public ImagenPrediction[] predictions;
    }

    public async System.Threading.Tasks.Task<Texture2D[]> GenerateImagesImagenAsync(
        string prompt,
        int count = 1,
        string aspect = "1:1",
        int? timeoutMs = null,
        string cacheKey = null,
        string storyId = null,
        int? sceneIndex = null)
    {
        if (executeFunction == null)
        {
            throw new Exception("Cloud Function executor is not configured for Imagen4Service");
        }

        var actualCount = Mathf.Clamp(count, 1, 4);
        var actualTimeout = timeoutMs ?? config.ImageTimeoutMs;
        var imageModel = string.IsNullOrEmpty(config.ImageModel) 
            ? "imagen-4.0-generate-preview-06-06" 
            : config.ImageModel;
        var cloudScriptPayload = new
        {
            prompt = prompt,
            sampleCount = actualCount,
            aspectRatio = aspect,
            imageModel = imageModel,
            timeoutMs = actualTimeout,
            cacheKey = cacheKey,
            storyId = storyId,
            sceneIndex = sceneIndex
        };

        var response = await executeFunction(config.ImageFunctionName, cloudScriptPayload);

        // Prefer CDN/Blob URLs if provided
        if (response?.images != null && response.images.Length > 0)
        {
            var textures = new List<Texture2D>();
            foreach (var info in response.images)
            {
                if (info == null || string.IsNullOrEmpty(info.url))
                    continue;

                try
                {
                    var data = await DownloadTexture(info.url);
                    if (data != null)
                    {
                        textures.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to download image from {info.url}: {ex.Message}");
                }
            }

            if (textures.Count > 0)
            {
                return textures.ToArray();
            }
        }

        if (response?.predictions == null || response.predictions.Length == 0)
        {
            throw new Exception("No predictions returned from Gemini CloudScript");
        }

        var list = new List<Texture2D>();
        foreach (var prediction in response.predictions)
        {
            if (prediction == null || string.IsNullOrEmpty(prediction.bytesBase64Encoded))
            {
                continue;
            }

            try
            {
                var bytes = Convert.FromBase64String(prediction.bytesBase64Encoded);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    list.Add(tex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to decode Gemini image prediction: {ex.Message}");
            }
        }

        if (list.Count == 0)
        {
            throw new Exception("All images failed to decode.");
        }

        return list.ToArray();
    }

    private async Task<Texture2D> DownloadTexture(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isHttpError || req.isNetworkError)
#endif
            {
                throw new Exception(req.error);
            }

            return DownloadHandlerTexture.GetContent(req);
        }
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "GeminiConfig", menuName = "StoryBook/API/Gemini Config")]
public class GeminiConfig : ScriptableObject
{
    [Header("API Configuration")]
    [SerializeField] private string model = "gemini-2.5-flash";
    [SerializeField] private string imageModel = "imagen-4.0-generate-preview-06-06";
    [SerializeField] private string textFunctionName = "generateGeminiText";
    [SerializeField] private string imageFunctionName = "generateGeminiImages";
    
    [Header("Request Settings")]
    [SerializeField] private int defaultTimeoutMs = 20000;
    [SerializeField] private int imageTimeoutMs = 60000;
    
    // Public properties
    public string Model => model;
    public int DefaultTimeoutMs => defaultTimeoutMs;
    public int ImageTimeoutMs => imageTimeoutMs;
    public string ImageModel => imageModel;
    public string TextFunctionName => textFunctionName;
    public string ImageFunctionName => imageFunctionName;
    
    // Validate configuration
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(imageModel) &&
               !string.IsNullOrEmpty(textFunctionName) && !string.IsNullOrEmpty(imageFunctionName);
    }
    
    // Validate in editor
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(model))
        {
            Debug.LogWarning($"[{name}] Please set model name");
        }
        
        if (string.IsNullOrEmpty(imageModel))
        {
            Debug.LogWarning($"[{name}] Please set image model name");
        }

        if (string.IsNullOrEmpty(textFunctionName))
        {
            Debug.LogWarning($"[{name}] Please set CloudScript text function name");
        }

        if (string.IsNullOrEmpty(imageFunctionName))
        {
            Debug.LogWarning($"[{name}] Please set CloudScript image function name");
        }
    }
}

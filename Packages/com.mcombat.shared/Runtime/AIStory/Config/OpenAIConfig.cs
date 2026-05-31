using UnityEngine;

[CreateAssetMenu(fileName = "OpenAIConfig", menuName = "StoryBook/API/OpenAI Config")]
public class OpenAIConfig : ScriptableObject
{
    [Header("API Configuration")]
    [SerializeField] private string apiKey = "YOUR_API_KEY";
    [SerializeField] private string textModel = "gpt-4o";
    [SerializeField] private string imageModel = "dall-e-3";
    
    [Header("Request Settings")]
    [SerializeField] private int defaultTimeoutMs = 20000;
    [SerializeField] private int imageTimeoutMs = 100000;
    
    [Header("Image Generation Settings")]
    [SerializeField] private int defaultImageCount = 1;
    [SerializeField] private string defaultSize = "1792x1024";
    [SerializeField] private string defaultQuality = "auto";
    [SerializeField] private string defaultStyle = "vivid";
    
    // Public properties
    public string ApiKey => apiKey;
    public string TextModel => textModel;
    public string ImageModel => imageModel;
    public int DefaultTimeoutMs => defaultTimeoutMs;
    public int ImageTimeoutMs => imageTimeoutMs;
    public int DefaultImageCount => defaultImageCount;
    public string DefaultSize => defaultSize;
    public string DefaultQuality => defaultQuality;
    public string DefaultStyle => defaultStyle;
    
    // Validate configuration
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY" && 
               !string.IsNullOrEmpty(textModel) && !string.IsNullOrEmpty(imageModel);
    }
    
    // Validate in editor
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_API_KEY")
        {
            Debug.LogWarning($"[{name}] Please set a valid API Key");
        }
        
        if (string.IsNullOrEmpty(textModel))
        {
            Debug.LogWarning($"[{name}] Please set text model name");
        }
        
        if (string.IsNullOrEmpty(imageModel))
        {
            Debug.LogWarning($"[{name}] Please set image model name");
        }
    }
}

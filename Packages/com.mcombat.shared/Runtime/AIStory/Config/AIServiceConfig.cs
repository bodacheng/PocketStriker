using UnityEngine;

public enum ImageStyle
{
    Photorealistic,     // 真实摄影风格
    Anime,              // 动漫风格
    Watercolor,         // 水彩画风格
    OilPainting,        // 油画风格
    PencilSketch,       // 铅笔素描风格
    DigitalArt,         // 数字艺术风格
    Cinematic,          // 电影风格
    Custom              // 自定义风格
}

public enum StoryStyle
{
    FairyTale,
    Adventure,
    SportsDrama,
    Comedy,
    DarkFantasy,
    Custom
}

public enum StoryFallbackTone
{
    FairyTale,
    Mature
}

[CreateAssetMenu(fileName = "AIServiceConfig", menuName = "StoryBook/API/AI Service Config")]
public class AIServiceConfig : ScriptableObject
{
    [Header("Model Selection")]
    [SerializeField] private AIModelType currentModel = AIModelType.Gemini;
    
    [Header("Model Configurations")]
    [SerializeField] private GeminiConfig geminiConfig;
    [SerializeField] private OpenAIConfig openAIConfig;
    
    [Header("Story Generation Settings")]
    [Tooltip("故事主旨列表，每次生成时会随机选择一个。可以自由添加任意数量的主题描述")]
    [SerializeField] [TextArea(2, 5)] private string[] storyThemes = new string[]
    {
        "关于亚洲小伙子光着膀子劳动的"
    };
    
    [Tooltip("连环画总页数")]
    [SerializeField] private int pageCount = 6;

    [Header("Story Style Settings")]
    [Tooltip("故事叙事风格，可与图片风格分别设置")]
    [SerializeField] private StoryStyle storyStyle = StoryStyle.FairyTale;

    [Tooltip("故事风格选择 Custom 时使用")]
    [SerializeField] [TextArea(2, 5)] private string customStoryStylePrompt = "";

    [Tooltip("故事生成额外要求，在故事风格基础上补充说明")]
    [SerializeField] [TextArea(2, 5)] private string additionalStoryRequirements = "";
    
    [Header("Image Style Settings")]
    [Tooltip("图片风格")]
    [SerializeField] private ImageStyle imageStyle = ImageStyle.Photorealistic;

    [Tooltip("图片风格选择 Custom 时使用")]
    [SerializeField] [TextArea(2, 5)] private string customImageStylePrompt = "";
    
    [Tooltip("图片生成额外要求，在风格基础上补充说明")]
    [SerializeField] [TextArea(2, 5)] private string additionalImageRequirements = "";
    
    [Tooltip("图片宽高比，例如：16:9, 1:1, 4:3")]
    [SerializeField] private string imageAspectRatio = "16:9";

    [Header("Fallback Story Config")]
    [Tooltip("选择备用主题配置的风格，用于 storyThemes 为空时自动生成主题")]
    [SerializeField] private StoryFallbackTone fallbackTone = StoryFallbackTone.FairyTale;
    [Tooltip("童话风格的地址，留空则使用默认内置数据")]
    [SerializeField] private string fairyTaleFallbackConfigAddress = "Config/FairyTaleFallbackConfig";
    [Tooltip("成熟/现实风格的地址，留空则使用默认内置数据")]
    [SerializeField] private string matureFallbackConfigAddress = "Config/MatureStoryFallbackConfig";
    [Tooltip("直接引用一个童话风格的配置，优先级高于地址")]
    [SerializeField] private FairyTaleFallbackConfig fairyTaleFallbackConfig;
    [Tooltip("直接引用一个成熟风格的配置，优先级高于地址")]
    [SerializeField] private MatureStoryFallbackConfig matureFallbackConfig;
    
    // Public properties
    public AIModelType CurrentModel => currentModel;
    public GeminiConfig GeminiConfig => geminiConfig;
    public OpenAIConfig OpenAIConfig => openAIConfig;
    public string[] StoryThemes => storyThemes;
    public int PageCount => pageCount;
    public StoryStyle StoryStyle => storyStyle;
    public string CustomStoryStylePrompt => customStoryStylePrompt;
    public string AdditionalStoryRequirements => additionalStoryRequirements;
    public ImageStyle ImageStyle => imageStyle;
    public string CustomImageStylePrompt => customImageStylePrompt;
    public string AdditionalImageRequirements => additionalImageRequirements;
    public string ImageAspectRatio => imageAspectRatio;
    public StoryFallbackTone FallbackTone => fallbackTone;
    public string FairyTaleFallbackConfigAddress => fairyTaleFallbackConfigAddress;
    public string MatureFallbackConfigAddress => matureFallbackConfigAddress;
    public FairyTaleFallbackConfig FairyTaleFallbackConfig => fairyTaleFallbackConfig;
    public MatureStoryFallbackConfig MatureFallbackConfig => matureFallbackConfig;
    
    public StoryFallbackConfigBase GetSelectedFallbackConfig()
    {
        return fallbackTone switch
        {
            StoryFallbackTone.Mature => matureFallbackConfig,
            _ => fairyTaleFallbackConfig
        };
    }

    public string GetSelectedFallbackAddress()
    {
        var address = fallbackTone switch
        {
            StoryFallbackTone.Mature => matureFallbackConfigAddress,
            _ => fairyTaleFallbackConfigAddress
        };

        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    public string GetStoryStylePrompt()
    {
        var baseStyle = storyStyle switch
        {
            StoryStyle.FairyTale => "warm fairy-tale picture-book storytelling, clear emotional beats, hopeful tone",
            StoryStyle.Adventure => "energetic adventure serial storytelling, escalating stakes, teamwork and discovery",
            StoryStyle.SportsDrama => "fast-paced sports drama storytelling, rivalry, training tension, comeback moments",
            StoryStyle.Comedy => "light comedic storytelling, playful reversals, expressive character reactions",
            StoryStyle.DarkFantasy => "dark fantasy storytelling, mysterious atmosphere, high stakes and bittersweet choices",
            StoryStyle.Custom => string.IsNullOrWhiteSpace(customStoryStylePrompt)
                ? "coherent illustrated story storytelling"
                : customStoryStylePrompt.Trim(),
            _ => "coherent illustrated story storytelling"
        };

        if (!string.IsNullOrWhiteSpace(additionalStoryRequirements))
        {
            baseStyle += $", {additionalStoryRequirements.Trim()}";
        }

        return baseStyle;
    }

    public string GetImageStylePrompt()
    {
        var baseStyle = imageStyle switch
        {
            ImageStyle.Photorealistic => "photorealistic style, high quality, natural colors",
            ImageStyle.Anime => "anime style, cel-shaded, vibrant colors",
            ImageStyle.Watercolor => "watercolor painting style, soft brushstrokes, artistic",
            ImageStyle.OilPainting => "oil painting style, rich textures, classical art",
            ImageStyle.PencilSketch => "pencil sketch style, detailed linework, monochrome",
            ImageStyle.DigitalArt => "digital art style, clean lines, modern illustration",
            ImageStyle.Cinematic => "cinematic style, dramatic lighting, movie quality",
            ImageStyle.Custom => string.IsNullOrWhiteSpace(customImageStylePrompt)
                ? "high quality illustration style"
                : customImageStylePrompt.Trim(),
            _ => "photorealistic style, high quality, natural colors"
        };

        if (!string.IsNullOrWhiteSpace(additionalImageRequirements))
        {
            baseStyle += $", {additionalImageRequirements.Trim()}";
        }

        return baseStyle;
    }
    
    /// <summary>
    /// Switch to a different AI model
    /// </summary>
    public void SetModel(AIModelType model)
    {
        currentModel = model;
        Debug.Log($"AI Model switched to: {model}");
    }
    
    /// <summary>
    /// Get the current model configuration
    /// </summary>
    public ScriptableObject GetCurrentConfig()
    {
        return currentModel switch
        {
            AIModelType.Gemini => geminiConfig,
            AIModelType.OpenAI => openAIConfig,
            _ => geminiConfig
        };
    }
    
    /// <summary>
    /// Check if the current model is properly configured
    /// </summary>
    public bool IsCurrentModelConfigured()
    {
        return currentModel switch
        {
            AIModelType.Gemini => geminiConfig != null && geminiConfig.IsValid(),
            AIModelType.OpenAI => openAIConfig != null && openAIConfig.IsValid(),
            _ => false
        };
    }
    
    /// <summary>
    /// Get available model types
    /// </summary>
    public AIModelType[] GetAvailableModels()
    {
        var available = new System.Collections.Generic.List<AIModelType>();
        
        if (geminiConfig != null && geminiConfig.IsValid())
            available.Add(AIModelType.Gemini);
            
        if (openAIConfig != null && openAIConfig.IsValid())
            available.Add(AIModelType.OpenAI);
            
        return available.ToArray();
    }
    
    // Validate in editor
    private void OnValidate()
    {
        if (currentModel == AIModelType.Gemini && geminiConfig == null)
        {
            Debug.LogWarning($"[{name}] Gemini config is not assigned");
        }
        
        if (currentModel == AIModelType.OpenAI && openAIConfig == null)
        {
            Debug.LogWarning($"[{name}] OpenAI config is not assigned");
        }
        
        if (!IsCurrentModelConfigured())
        {
            Debug.LogWarning($"[{name}] Current model ({currentModel}) is not properly configured");
        }

        if (GetSelectedFallbackConfig() == null && string.IsNullOrWhiteSpace(GetSelectedFallbackAddress()))
        {
            Debug.LogWarning($"[{name}] Fallback story config is not set for tone {fallbackTone}, will use built-in defaults.");
        }
    }
}

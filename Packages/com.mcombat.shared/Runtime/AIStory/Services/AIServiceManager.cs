using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Manages AI service switching between different providers (Gemini, OpenAI, etc.)
/// </summary>
public class AIServiceManager : MonoBehaviour
{
    [SerializeField] private string configAddress = "Config/AIServiceConfig";
    [SerializeField] private string fallbackThemeConfigAddress = "Config/FairyTaleFallbackConfig";
    private AIServiceConfig serviceConfig;
    private AsyncOperationHandle<AIServiceConfig> serviceConfigHandle;
    private StoryFallbackConfigBase fallbackThemeConfig;
    private IAIClient currentClient;
    private GeminiClient geminiClient;
    private OpenAIClient openAIClient;
    private List<StoryInfo.CharacterProfile> currentStoryCharacters = new List<StoryInfo.CharacterProfile>();
    private List<StoryInfo.LocationProfile> currentStoryLocations = new List<StoryInfo.LocationProfile>();
    private StoryInfo.StoryStyleGuide currentStoryStyleGuide;
    private const int StoryTextCharactersPerPageLimit = 200;
    private const string StoryCacheKeyPrefix = "ai_story_v1";
    private static readonly char[] StoryTextSplitCandidates =
    {
        ' ', '\t', '\r', '\n',
        ',', '.', ';', ':',
        '，', '。', '！', '？', '、', '；', '：',
        '!', '?', '…', '—', '-', '‧', '･'
    };
    private static readonly object GeneratedStoryLock = new object();
    private static readonly string[] DefaultImageNegativeTokens = new[]
    {
        "speech bubble",
        "speech bubbles",
        "dialogue bubble",
        "text bubble",
        "text bubbles",
        "onscreen text",
        "text overlay",
        "text overlays",
        "caption text",
        "subtitles",
        "comic lettering",
        "watermark"
    };
    private static StoryInfo _cachedGeneratedStory;
    private static string _cachedGeneratedStoryKey;
    private static bool _cachedGeneratedStoryShown;
    private static UniTaskCompletionSource<StoryInfo> _generatedStorySource;
    private static string _generatedStoryKey;
    private static readonly object ProverbHistoryLock = new object();
    private static readonly List<string> ProverbHistory = new List<string>();
    private static readonly HashSet<string> ProverbHistoryKeys = new HashSet<string>(StringComparer.Ordinal);
    private static SystemLanguage ProverbHistoryLanguage = SystemLanguage.Unknown;
    private string currentStoryCacheKey;
    
    // Properties
    public IAIClient CurrentClient => currentClient;
    public AIModelType CurrentModel => serviceConfig?.CurrentModel ?? AIModelType.Gemini;
    public bool IsConfigured => currentClient?.IsConfigured ?? false;
    public string CurrentProviderName => currentClient?.ProviderName ?? "None";
    
    /// <summary>
    /// Initialize all AI clients
    /// </summary>
    public void InitializeClients()
    {
        if (serviceConfig == null)
        {
            Debug.LogError("AIServiceConfig is not assigned!");
            return;
        }
        
        // Initialize Gemini client
        if (serviceConfig.GeminiConfig != null)
        {
            geminiClient = new GeminiClient(serviceConfig.GeminiConfig);
            Debug.Log($"Gemini client initialized: {geminiClient.IsConfigured}");
        }
        
        // Initialize OpenAI client
        if (serviceConfig.OpenAIConfig != null)
        {
            openAIClient = new OpenAIClient(serviceConfig.OpenAIConfig);
            Debug.Log($"OpenAI client initialized: {openAIClient.IsConfigured}");
        }
        
        // Set initial client
        SwitchToModel(serviceConfig.CurrentModel);
    }
    
    /// <summary>
    /// Switch to a different AI model
    /// </summary>
    public bool SwitchToModel(AIModelType modelType)
    {
        if (serviceConfig == null)
        {
            return false;
        }
        
        IAIClient newClient = modelType switch
        {
            AIModelType.Gemini => geminiClient,
            AIModelType.OpenAI => openAIClient,
            _ => null
        };
        
        if (newClient == null)
        {
            return false;
        }
        
        if (!newClient.IsConfigured)
        {
            return false;
        }
        
        currentClient = newClient;
        serviceConfig.SetModel(modelType);
        
        Debug.Log($"Switched to {modelType} provider: {newClient.ProviderName}");
        
        return true;
    }
    
    /// <summary>
    /// Get available model types
    /// </summary>
    public AIModelType[] GetAvailableModels()
    {
        if (serviceConfig == null) return new AIModelType[0];
        return serviceConfig.GetAvailableModels();
    }
    
    /// <summary>
    /// Check if a specific model is available
    /// </summary>
    public bool IsModelAvailable(AIModelType modelType)
    {
        var available = GetAvailableModels();
        return System.Array.IndexOf(available, modelType) >= 0;
    }
    
    /// <summary>
    /// Get the current client for direct access (for backward compatibility)
    /// </summary>
    public T GetClient<T>() where T : class, IAIClient
    {
        return currentClient as T;
    }
    
    /// <summary>
    /// Get Gemini client specifically
    /// </summary>
    public GeminiClient GetGeminiClient()
    {
        return geminiClient;
    }
    
    /// <summary>
    /// Get OpenAI client specifically
    /// </summary>
    public OpenAIClient GetOpenAIClient()
    {
        return openAIClient;
    }
    
    /// <summary>
    /// Send a text prompt using the current AI model
    /// </summary>
    public System.Threading.Tasks.Task<string> AskAsync(string question, int? timeoutMs = null)
    {
        if (currentClient == null)
        {
            return System.Threading.Tasks.Task.FromResult<string>(null);
        }
        
        return currentClient.AskAsync(question, timeoutMs);
    }
    
    /// <summary>
    /// Generate a short proverb that matches the current language settings.
    /// </summary>
    public async Task<string> GenerateLocalizedProverbAsync(int? timeoutMs = null)
    {
        await EnsureServiceInitializedAsync();
        
        if (!IsConfigured)
        {
            return null;
        }
        
        var targetLanguage = GetConfiguredLanguage();
        var prompt = BuildProverbPrompt(targetLanguage, GetProverbHistorySnapshot(targetLanguage));
        
        try
        {
            var result = await AskAsync(prompt, timeoutMs);
            var proverb = ExtractSingleLineText(result);
            RecordProverb(targetLanguage, proverb);
            return proverb;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIServiceManager] Failed to generate proverb: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Generate images using the current AI model
    /// </summary>
    public System.Threading.Tasks.Task<Texture2D[]> GeneratePic(string prompt, int? count = null, string aspectRatio = null)
    {
        if (currentClient == null)
        {
            return System.Threading.Tasks.Task.FromResult<Texture2D[]>(null);
        }
        
        return currentClient.GeneratePic(prompt, count, aspectRatio);
    }
    
    public async UniTask<StoryInfo> LoadAIStory()
    {
        await EnsureServiceInitializedAsync();
        
        // 如果没有配置成功，尽量复用已有的缓存故事
        if (!IsConfigured)
        {
            var cacheKey = BuildStoryCacheKey(null);
            lock (GeneratedStoryLock)
            {
                return string.Equals(_cachedGeneratedStoryKey, cacheKey, StringComparison.Ordinal) ? _cachedGeneratedStory : null;
            }
        }
        
        return await GetOrCreateAIStoryAsync();
    }

    private async UniTask EnsureServiceInitializedAsync()
    {
        if (serviceConfig == null)
        {
            Debug.Log("[AIServiceManager] Loading AIServiceConfig from Addressables...");
            if (!serviceConfigHandle.IsValid())
            {
                serviceConfigHandle = Addressables.LoadAssetAsync<AIServiceConfig>(configAddress);
            }
            await serviceConfigHandle.Task;
            
            if (serviceConfigHandle.Status == AsyncOperationStatus.Succeeded)
            {
                serviceConfig = serviceConfigHandle.Result;
                Debug.Log("[AIServiceManager] AIServiceConfig loaded successfully");
            }
            else
            {
                Debug.LogError($"[AIServiceManager] Failed to load AIServiceConfig from Addressables. Status: {serviceConfigHandle.Status}");
                if (serviceConfigHandle.IsValid())
                    Addressables.Release(serviceConfigHandle);
                serviceConfigHandle = default;
                return;
            }
        }
        
        if (fallbackThemeConfig == null)
        {
            await LoadFallbackThemeConfigAsync();
        }
        
        if ((currentClient == null || !IsConfigured) && serviceConfig != null)
        {
            InitializeClients();
            Debug.Log($"[AIServiceManager] Clients initialized, Current Model: {CurrentModel}, IsConfigured: {IsConfigured}");
        }
    }

    private void OnDestroy()
    {
        if (serviceConfigHandle.IsValid())
        {
            Addressables.Release(serviceConfigHandle);
        }
    }

    private async UniTask LoadFallbackThemeConfigAsync()
    {
        var tone = GetFallbackTone();

        var inlineConfig = serviceConfig?.GetSelectedFallbackConfig();
        if (inlineConfig != null)
        {
            fallbackThemeConfig = inlineConfig.CreateMergedInstance() ?? CreateDefaultFallbackConfig(tone);
            Debug.Log($"[AIServiceManager] Using inline fallback config ({tone}) from AIServiceConfig");
            return;
        }

        string address = ResolveFallbackConfigAddress(tone);
        if (string.IsNullOrWhiteSpace(address))
        {
            fallbackThemeConfig = CreateDefaultFallbackConfig(tone);
            Debug.LogWarning("[AIServiceManager] Fallback config address is empty, using default values.");
            return;
        }

        Debug.Log($"[AIServiceManager] Loading fallback config ({tone}) from Addressables at {address}...");
        var fallbackHandle = Addressables.LoadAssetAsync<StoryFallbackConfigBase>(address);
        try
        {
            await fallbackHandle.Task;

            if (fallbackHandle.Status == AsyncOperationStatus.Succeeded && fallbackHandle.Result != null)
            {
                fallbackThemeConfig = MergeFallbackConfig(fallbackHandle.Result, tone);
                Debug.Log("[AIServiceManager] Fallback config loaded successfully");
            }
            else
            {
                Debug.LogWarning($"[AIServiceManager] Failed to load fallback config from Addressables ({address}), status: {fallbackHandle.Status}. Using default values.");
                fallbackThemeConfig = CreateDefaultFallbackConfig(tone);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIServiceManager] Exception while loading fallback config: {ex.Message}. Using default values.");
            fallbackThemeConfig = CreateDefaultFallbackConfig(tone);
        }
        finally
        {
            if (fallbackHandle.IsValid())
            {
                Addressables.Release(fallbackHandle);
            }
        }
    }

    private UniTask<StoryInfo> GetOrCreateAIStoryAsync()
    {
        var cacheKey = BuildStoryCacheKey(null);
        UniTaskCompletionSource<StoryInfo> generationSource = null;
        lock (GeneratedStoryLock)
        {
            if (_cachedGeneratedStory != null && !string.Equals(_cachedGeneratedStoryKey, cacheKey, StringComparison.Ordinal))
            {
                _cachedGeneratedStory = null;
                _cachedGeneratedStoryKey = null;
                _cachedGeneratedStoryShown = false;
            }

            if (_cachedGeneratedStory != null && !_cachedGeneratedStoryShown)
            {
                return UniTask.FromResult(_cachedGeneratedStory);
            }
            
            if (_cachedGeneratedStory != null && _cachedGeneratedStoryShown)
            {
                _cachedGeneratedStory = null;
                _cachedGeneratedStoryKey = null;
                _cachedGeneratedStoryShown = false;
            }
            
            if (_generatedStorySource != null && string.Equals(_generatedStoryKey, cacheKey, StringComparison.Ordinal))
            {
                return _generatedStorySource.Task;
            }
            
            _generatedStorySource = new UniTaskCompletionSource<StoryInfo>();
            _generatedStoryKey = cacheKey;
            generationSource = _generatedStorySource;
        }
        
        GenerateAIStoryInternalAsync(generationSource, cacheKey).Forget();
        return generationSource.Task;
    }

    private async UniTaskVoid GenerateAIStoryInternalAsync(UniTaskCompletionSource<StoryInfo> generationSource, string cacheKey)
    {
        StoryInfo story = null;
        Exception exception = null;
        
        try
        {
            story = await GenerateAIStoryAsync(null);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        
        lock (GeneratedStoryLock)
        {
            var isCurrentGeneration = _generatedStorySource == generationSource &&
                                      string.Equals(_generatedStoryKey, cacheKey, StringComparison.Ordinal);
            if (isCurrentGeneration)
            {
                _generatedStorySource = null;
                _generatedStoryKey = null;
            }
            
            if (isCurrentGeneration && exception == null && story != null)
            {
                _cachedGeneratedStory = story;
                _cachedGeneratedStoryKey = cacheKey;
                _cachedGeneratedStoryShown = false;
            }
        }
        
        if (exception != null)
        {
            generationSource.TrySetException(exception);
        }
        else
        {
            generationSource.TrySetResult(story);
        }
    }

    public void MarkAIStoryAsShown()
    {
        lock (GeneratedStoryLock)
        {
            if (_cachedGeneratedStory != null)
            {
                _cachedGeneratedStoryShown = true;
            }
        }
    }

    public void MarkEventStoryAsShown()
    {
        MarkAIStoryAsShown();
    }
    
    /// <summary>
    /// 使用AIStory系统生成StoryInfo对象
    /// 
    /// 此方法集成了AIStory系统的文本生成和图片生成功能，创建一个完整的StoryInfo对象。
    /// 生成的StoryInfo对象可以用于任何需要故事内容的场景，UI显示由调用方自行处理。
    /// 
    /// 使用示例：
    /// <code>
    /// // 使用默认配置生成
    /// var storyInfo = await aiService.GenerateAIStoryAsync(null);
    /// 
    /// // 使用自定义提示词
    /// var storyInfo = await aiService.GenerateAIStoryAsync("请生成一个关于...的故事");
    /// </code>
    /// </summary>
    /// <param name="storyPrompt">可选的自定义故事提示词，如果为null则使用配置中的主题自动生成</param>
    /// <returns>生成的StoryInfo对象，如果生成失败则返回null</returns>
    public async UniTask<StoryInfo> GenerateAIStoryAsync(string storyPrompt)
    {
        try
        {
            // 检查AI服务是否可用
            if (serviceConfig == null || currentClient == null || !IsConfigured)
            {
                Debug.LogWarning("AI Service is not available or not configured");
                return null;
            }

            // 构建故事生成提示词
            string prompt = BuildStoryPrompt(storyPrompt);
            currentStoryCacheKey = BuildStoryCacheKey(storyPrompt);
            
            // 使用AI生成故事文本（支持缓存）
            string storyText = await AskStoryWithCacheAsync(prompt, currentStoryCacheKey);
            
            if (string.IsNullOrEmpty(storyText))
            {
                Debug.LogError("Failed to generate story text from AI");
                return null;
            }
            
            // 输出AI返回的原始文本用于调试
            Debug.Log($"[AI Story] Raw response from AI:\n{storyText}");

            // 获取期望的页数
            int expectedPageCount = serviceConfig?.PageCount ?? 6;
            
            // 解析AI生成的故事文本，提取场景、角色和风格设定
            var parsedStory = ParseStoryText(storyText, expectedPageCount);
            
            // 验证解析结果
            if (parsedStory == null || parsedStory.Scenes == null || parsedStory.Scenes.Count == 0)
            {
                Debug.LogError("[AI Story] Failed to parse story scenes from AI response");
                return null;
            }
            
            Debug.Log($"[AI Story] Successfully parsed {parsedStory.Scenes.Count} scenes (expected: {expectedPageCount})");
            
            currentStoryCharacters = parsedStory.Characters ?? new List<StoryInfo.CharacterProfile>();
            currentStoryLocations = parsedStory.Locations ?? new List<StoryInfo.LocationProfile>();
            currentStoryStyleGuide = parsedStory.StyleGuide ?? new StoryInfo.StoryStyleGuide();
            
            // 为每个场景生成对应的图片
            await GenerateStoryImagesAsync(parsedStory.Scenes);

            if (!IsAIStoryComplete(parsedStory.Scenes))
            {
                Debug.LogWarning("[AI Story] Incomplete AI story (missing text or images). Skipping story display.");
                return null;
            }
            
            // 创建StoryInfo对象
            var storyInfo = ScriptableObject.CreateInstance<StoryInfo>();
            storyInfo.Characters = currentStoryCharacters;
            storyInfo.Locations = currentStoryLocations;
            storyInfo.StyleGuide = currentStoryStyleGuide;
            storyInfo.StoryScenes = parsedStory.Scenes;
            
            Debug.Log($"Successfully generated AI story with {parsedStory.Scenes.Count} scenes");
            return storyInfo;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating AI story: {ex.Message}");
            return null;
        }
    }
        
    /// <summary>
    /// 构建故事生成的提示词
    /// </summary>
    private string BuildStoryPrompt(string customPrompt)
    {
        if (!string.IsNullOrEmpty(customPrompt))
        {
            return customPrompt;
        }
        
        if (serviceConfig == null)
        {
            Debug.LogError("[BuildStoryPrompt] serviceConfig is null");
            return "";
        }
        
        int pageCount = serviceConfig.PageCount;
        string storyTheme = GetStoryThemeFromConfig();
        string storyStyle = serviceConfig.GetStoryStylePrompt();
        string imageStyle = GetImageStyleFromConfig();
        
        var promptBuilder = new StringBuilder();
        SystemLanguage targetLanguage = GetConfiguredLanguage();
        string languageInstruction = BuildLanguageInstruction(targetLanguage);
        
        string storytellerIntro;
        string themeInstruction;
        string jsonInstruction;
        string ensureHeader;
        string[] ensureItems;
        string templateHeader;
        string[] templateLines;
        string storyStyleLineFormat;
        string imageStyleLineFormat;
        string finalReminder;
        
        switch (targetLanguage)
        {
            case SystemLanguage.Chinese:
                storytellerIntro = "你是一名专业的多页连环画编剧与美术总监。";
                themeInstruction = $"围绕主题“{storyTheme}”创作一个由 {pageCount} 个连续场景组成的故事。";
                jsonInstruction = "请输出严格的 JSON 数据，禁止添加注释、额外说明或 markdown。";
                ensureHeader = "请确保：";
                ensureItems = new[]
                {
                    $"- 场景数量必须正好为 {pageCount}，index 从 1 开始，按顺序排列。",
                    "- 对所有会重复出现的角色提供一致的角色卡片，在文中和 scenes.characters 列表中复用相同的 id。",
                    "- 对关键场景地点提供地点卡片，在每个场景中引用 locations 的 id 以保持背景一致。",
                    "- 角色描述需包含发型、五官、体态、肤色、服装和可辨识的随身物品。",
                    "- 地点描述需包含时间、氛围、色调、重要道具。",
                    "- 为整部作品给出统一的 style（artDirection、palette、camera、lighting、keywords、negativeKeywords）以保证画风统一。",
                    "- 每个场景给出 title、description（描述画面）、setting（引用地点并解释时间/情境）、locationId（引用 locations.id）、characters（角色 id 列表）、mood、importantObjects、camera、lighting、visualPrompt、negativePrompt。",
                    "- dialogues 是数组，包含 speaker 与 text，用于剧情对话。若无对话可返回空数组。",
                    "- visualPrompt 用简洁英文或中英文混合描述画面要素，便于直接用于图像生成。",
                    "- 场景的 description、setting 与 dialogues 必须以角色行动、互动、情绪与剧情推进为核心，可以适度融入感官细节，禁止描述或暗示角色外形与服饰（相关信息仅能保留在角色卡片中）。",
                    "- description 与 dialogues 需表现为连贯的叙事句，交代时间推移和因果关系，避免使用“画面包含…”“主要元素：”等提示词式片段。",
                    "- negativePrompt 描述应避免的视觉内容和错误。",
                    "- 全文除 JSON 以外不输出任何内容。"
                };
                templateHeader = "请按照以下 JSON 模板返回：";
                templateLines = new[]
                {
                    "{",
                    "  \"characters\": [",
                    "    {",
                    "      \"id\": \"hero\",",
                    "      \"name\": \"角色姓名\",",
                    "      \"appearance\": \"身材、发型、肤色等\",",
                    "      \"outfit\": \"服装、配饰\",",
                    "      \"personality\": \"性格或当前情绪\",",
                    "      \"visualTags\": \"关键视觉要素\"",
                    "    }",
                    "  ],",
                    "  \"locations\": [",
                    "    {",
                    "      \"id\": \"warehouse\",",
                    "      \"name\": \"地点名称\",",
                    "      \"description\": \"背景细节\",",
                    "      \"palette\": \"主色调\",",
                    "      \"timeOfDay\": \"时间\",",
                    "      \"atmosphere\": \"氛围\"",
                    "    }",
                    "  ],",
                    "  \"style\": {",
                    "    \"artDirection\": \"整体绘画风格\",",
                    "    \"palette\": \"整体配色\",",
                    "    \"camera\": \"常用镜头语言\",",
                    "    \"lighting\": \"整体光影\",",
                    "    \"keywords\": \"需要强化的视觉关键词\",",
                    "    \"negativeKeywords\": \"需要避免的元素\"",
                    "  },",
                    "  \"scenes\": [",
                    "    {",
                    "      \"index\": 1,",
                    "      \"title\": \"场景标题\",",
                    "      \"description\": \"画面描述\",",
                    "      \"setting\": \"引用地点 id 并补充细节\",",
                    "      \"locationId\": \"warehouse\",",
                    "      \"characters\": [\"hero\"],",
                    "      \"mood\": \"情绪\",",
                    "      \"importantObjects\": \"关键物件\",",
                    "      \"camera\": \"镜头语言\",",
                    "      \"lighting\": \"光影\",",
                    "      \"visualPrompt\": \"图像生成提示\",",
                    "      \"negativePrompt\": \"需避免的元素\",",
                    "      \"dialogues\": [",
                    "        { \"speaker\": \"角色姓名\", \"text\": \"具体台词\" }",
                    "      ]",
                    "    }",
                    "  ]",
                    "}"
                };
                storyStyleLineFormat = "故事叙事风格需要贴合：{0}。";
                imageStyleLineFormat = "插图视觉风格需要贴合：{0}。";
                finalReminder = "请注意保持角色造型、服饰、背景在所有场景中的一致性，并确保 JSON 可被解析。整体叙事需自然连贯，故事文本中严禁描述角色外貌与服饰细节，也不要出现提示词式语句。";
                break;
            case SystemLanguage.Japanese:
                storytellerIntro = "あなたはプロの連続挿絵物語の脚本家兼アートディレクターです。";
                themeInstruction = $"テーマ「{storyTheme}」をもとに、{pageCount} 個の連続したシーンで構成される物語を作成してください。";
                jsonInstruction = "コメントや追加説明、markdown を入れず、厳密な JSON 形式で出力してください。";
                ensureHeader = "必ず次の点を守ってください:";
                ensureItems = new[]
                {
                    $"- シーン数は必ず {pageCount} 個、index は 1 から連番で並べてください。",
                    "- 登場を繰り返すキャラクターには一貫したキャラクターシートを用意し、本文と scenes.characters で同じ id を再利用してください。",
                    "- 重要な舞台にはロケーションシートを作成し、各シーンで locationId として id を参照して背景を揃えてください。",
                    "- キャラクター説明には髪型・顔立ち・体格・肌の色・衣装・識別できる小物を含めてください。",
                    "- ロケーション説明には時間帯・雰囲気・色調・重要なオブジェクトを含めてください。",
                    "- 作品全体のスタイル（artDirection, palette, camera, lighting, keywords, negativeKeywords）を統一してください。",
                    "- 各シーンには title, description（画面描写）, setting（ロケーション id と状況説明）, locationId, characters（id リスト）, mood, importantObjects, camera, lighting, visualPrompt, negativePrompt を含めてください。",
                    "- dialogues は speaker と text を持つ要素の配列です。会話がない場合は空配列を返してください。",
                    "- visualPrompt は画像生成のための要素を簡潔な英語またはバイリンガルで記述してください。",
                    "- description と setting、dialogues はキャラクターの行動・感情・物語の進行に専念し、外見・衣装・身体的特徴を描写したり暗示したりしないでください（それらはキャラクターシートのみで扱います）。",
                    "- description と dialogues は流れるような叙述文で構成し、時間の流れや因果を示しつつ、\"〜を描写せよ\" \"要素:\" のようなプロンプト的表現を避けてください。",
                    "- negativePrompt は避けたい要素や誤りを示してください。",
                    "- JSON 本文以外は何も出力しないでください。"
                };
                templateHeader = "次の JSON テンプレートに従って出力してください:";
                templateLines = new[]
                {
                    "{",
                    "  \"characters\": [",
                    "    {",
                    "      \"id\": \"hero\",",
                    "      \"name\": \"キャラクター名\",",
                    "      \"appearance\": \"体格・髪型・肌の色など\",",
                    "      \"outfit\": \"衣装とアクセサリー\",",
                    "      \"personality\": \"性格または現在の感情\",",
                    "      \"visualTags\": \"重要なビジュアルタグ\"",
                    "    }",
                    "  ],",
                    "  \"locations\": [",
                    "    {",
                    "      \"id\": \"warehouse\",",
                    "      \"name\": \"ロケーション名\",",
                    "      \"description\": \"背景の詳細\",",
                    "      \"palette\": \"主要な配色\",",
                    "      \"timeOfDay\": \"時間帯\",",
                    "      \"atmosphere\": \"雰囲気\"",
                    "    }",
                    "  ],",
                    "  \"style\": {",
                    "    \"artDirection\": \"全体のアートディレクション\",",
                    "    \"palette\": \"全体の配色\",",
                    "    \"camera\": \"よく使うカメラ表現\",",
                    "    \"lighting\": \"全体のライティング\",",
                    "    \"keywords\": \"強調したいビジュアルキーワード\",",
                    "    \"negativeKeywords\": \"避けるべき要素\"",
                    "  },",
                    "  \"scenes\": [",
                    "    {",
                    "      \"index\": 1,",
                    "      \"title\": \"シーンタイトル\",",
                    "      \"description\": \"画面描写\",",
                    "      \"setting\": \"ロケーション id と状況説明\",",
                    "      \"locationId\": \"warehouse\",",
                    "      \"characters\": [\"hero\"],",
                    "      \"mood\": \"感情\",",
                    "      \"importantObjects\": \"重要なオブジェクト\",",
                    "      \"camera\": \"カメラ表現\",",
                    "      \"lighting\": \"ライティング\",",
                    "      \"visualPrompt\": \"画像生成プロンプト\",",
                    "      \"negativePrompt\": \"避けるべき要素\",",
                    "      \"dialogues\": [",
                    "        { \"speaker\": \"キャラクター名\", \"text\": \"台詞\" }",
                    "      ]",
                    "    }",
                    "  ]",
                    "}"
                };
                storyStyleLineFormat = "物語の語り口は {0} に合わせてください。";
                imageStyleLineFormat = "挿絵のビジュアルスタイルは {0} に合わせてください。";
                finalReminder = "全シーンでキャラクターデザイン・衣装・背景の整合性を保ち、JSON が正しく解析できるようにしてください。ストーリーは自然な叙述で統一し、本文には外見・衣装の描写やプロンプト的な言い回しを入れないこと。";
                break;
            default:
                storytellerIntro = "You are a professional multi-page picture book writer and art director.";
                themeInstruction = $"Create a story composed of {pageCount} consecutive scenes around the theme \"{storyTheme}\".";
                jsonInstruction = "Return strictly formatted JSON with no comments, extra explanations, or markdown.";
                ensureHeader = "Make sure that:";
                ensureItems = new[]
                {
                    $"- The number of scenes must be exactly {pageCount}, with indexes starting at 1 in sequential order.",
                    "- Provide consistent character sheets for any recurring characters, and reuse the same ids in the narrative and scenes.characters list.",
                    "- Provide location sheets for key settings and reference their ids in each scene's locationId to keep backgrounds consistent.",
                    "- Character descriptions must cover hairstyle, facial features, body type, skin tone, wardrobe, and recognizable props.",
                    "- Location descriptions must cover time of day, atmosphere, color palette, and important props.",
                    "- Supply a unified style block (artDirection, palette, camera, lighting, keywords, negativeKeywords) to keep the art coherent.",
                    "- Each scene must include title, description (visual composition), setting (reference location id and context), locationId, characters (id list), mood, importantObjects, camera, lighting, visualPrompt, negativePrompt.",
                    "- dialogues is an array of entries with speaker and text. Return an empty array if the scene has no dialogue.",
                    "- visualPrompt should use concise English or bilingual hints describing the imagery for text-to-image generation.",
                    "- Use scene description, setting, and dialogue to highlight character actions, interactions, emotions, and plot progression; do not describe or hint at character appearances, outfits, or physical attributes (those belong exclusively in the character sheets).",
                    "- Write descriptions and dialogue as flowing narrative prose that conveys pacing and causality; avoid prompt-like phrases such as \"show...\", \"elements:\" or keyword lists.",
                    "- negativePrompt should specify visual mistakes or elements to avoid.",
                    "- Output nothing outside of the JSON body."
                };
                templateHeader = "Return JSON following this template:";
                templateLines = new[]
                {
                    "{",
                    "  \"characters\": [",
                    "    {",
                    "      \"id\": \"hero\",",
                    "      \"name\": \"Character name\",",
                    "      \"appearance\": \"Body type, hairstyle, skin tone, etc.\",",
                    "      \"outfit\": \"Wardrobe and accessories\",",
                    "      \"personality\": \"Personality or current emotion\",",
                    "      \"visualTags\": \"Key visual tags\"",
                    "    }",
                    "  ],",
                    "  \"locations\": [",
                    "    {",
                    "      \"id\": \"warehouse\",",
                    "      \"name\": \"Location name\",",
                    "      \"description\": \"Background details\",",
                    "      \"palette\": \"Primary colors\",",
                    "      \"timeOfDay\": \"Time of day\",",
                    "      \"atmosphere\": \"Mood\"",
                    "    }",
                    "  ],",
                    "  \"style\": {",
                    "    \"artDirection\": \"Overall art direction\",",
                    "    \"palette\": \"Overall palette\",",
                    "    \"camera\": \"Common camera language\",",
                    "    \"lighting\": \"Overall lighting\",",
                    "    \"keywords\": \"Visual keywords to reinforce\",",
                    "    \"negativeKeywords\": \"Elements to avoid\"",
                    "  },",
                    "  \"scenes\": [",
                    "    {",
                    "      \"index\": 1,",
                    "      \"title\": \"Scene title\",",
                    "      \"description\": \"Visual description\",",
                    "      \"setting\": \"Reference location id and context\",",
                    "      \"locationId\": \"warehouse\",",
                    "      \"characters\": [\"hero\"],",
                    "      \"mood\": \"Emotion\",",
                    "      \"importantObjects\": \"Important props\",",
                    "      \"camera\": \"Camera language\",",
                    "      \"lighting\": \"Lighting\",",
                    "      \"visualPrompt\": \"Image generation prompt\",",
                    "      \"negativePrompt\": \"Elements to avoid\",",
                    "      \"dialogues\": [",
                    "        { \"speaker\": \"Character name\", \"text\": \"Dialogue line\" }",
                    "      ]",
                    "    }",
                    "  ]",
                    "}"
                };
                storyStyleLineFormat = "Match the narrative style: {0}.";
                imageStyleLineFormat = "Match the illustration style: {0}.";
                finalReminder = "Maintain consistent character designs, outfits, and backgrounds across all scenes, ensure the JSON is valid, and keep the story text natural and flowing without appearance descriptions or prompt-like phrasing.";
                break;
        }
        
        promptBuilder.AppendLine(storytellerIntro);
        promptBuilder.AppendLine(themeInstruction);
        promptBuilder.AppendLine(jsonInstruction);
        if (!string.IsNullOrEmpty(languageInstruction))
        {
            promptBuilder.AppendLine(languageInstruction);
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine(ensureHeader);
        foreach (var line in ensureItems)
        {
            promptBuilder.AppendLine(line);
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine(templateHeader);
        foreach (var line in templateLines)
        {
            promptBuilder.AppendLine(line);
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine(string.Format(storyStyleLineFormat, storyStyle));
        promptBuilder.AppendLine(string.Format(imageStyleLineFormat, imageStyle));
        promptBuilder.AppendLine(finalReminder);
        
        return promptBuilder.ToString();
    }
    
    /// <summary>
    /// 解析AI生成的故事文本，并确保返回指定数量的场景
    /// </summary>
    private ParsedStoryResult ParseStoryText(string storyText, int expectedSceneCount)
    {
        if (string.IsNullOrEmpty(storyText))
        {
            Debug.LogError("[AI Story Parse] Story text is null or empty");
            return new ParsedStoryResult
            {
                Scenes = EnsureSceneCount(CreatePlaceholderScenes(expectedSceneCount), expectedSceneCount),
                Characters = new List<StoryInfo.CharacterProfile>(),
                Locations = new List<StoryInfo.LocationProfile>(),
                StyleGuide = new StoryInfo.StoryStyleGuide()
            };
        }
        
        string cleanedText = CleanJsonFromMarkdown(storyText);
        var parsedStory = TryParseJsonStory(cleanedText);
        
        if (parsedStory == null || parsedStory.Scenes == null || parsedStory.Scenes.Count == 0)
        {
            Debug.LogWarning("[AI Story Parse] Falling back to simple text parsing");
            var fallbackScenes = EnsureSceneCount(FallbackParseStoryText(cleanedText), expectedSceneCount);
            return new ParsedStoryResult
            {
                Scenes = fallbackScenes,
                Characters = new List<StoryInfo.CharacterProfile>(),
                Locations = new List<StoryInfo.LocationProfile>(),
                StyleGuide = new StoryInfo.StoryStyleGuide()
            };
        }
        
        parsedStory.Scenes = EnsureSceneCount(parsedStory.Scenes, expectedSceneCount);
        parsedStory.Characters ??= new List<StoryInfo.CharacterProfile>();
        parsedStory.Locations ??= new List<StoryInfo.LocationProfile>();
        parsedStory.StyleGuide ??= new StoryInfo.StoryStyleGuide();
        
        Debug.Log($"[AI Story Parse] Final: {parsedStory.Scenes.Count} scenes");
        return parsedStory;
    }
    
    private ParsedStoryResult TryParseJsonStory(string jsonText)
    {
        try
        {
            var storyData = JsonUtility.FromJson<AIStoryData>(jsonText);
            if (storyData == null)
            {
                return null;
            }
            
            var result = new ParsedStoryResult
            {
                Characters = storyData.characters?.Select(ConvertToCharacterProfile).Where(c => c != null).ToList(),
                Locations = storyData.locations?.Select(ConvertToLocationProfile).Where(l => l != null).ToList(),
                StyleGuide = ConvertToStyleGuide(storyData.style),
                Scenes = new List<StoryInfo.StoryScene>()
            };
            
            if (storyData.scenes != null)
            {
                foreach (var sceneData in storyData.scenes)
                {
                    var scene = ConvertToStoryScene(sceneData);
                    if (scene != null)
                    {
                        result.Scenes.Add(scene);
                    }
                }
                
                Debug.Log($"[AI Story Parse] Parsed {result.Scenes.Count} scenes from JSON");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AI Story Parse] JSON parsing failed: {ex.Message}");
            return null;
        }
    }
    
    private StoryInfo.CharacterProfile ConvertToCharacterProfile(AICharacterData data)
    {
        if (data == null)
        {
            return null;
        }
        
        return new StoryInfo.CharacterProfile
        {
            Id = data.id,
            DisplayName = data.name,
            Description = data.appearance,
            Outfit = data.outfit,
            Personality = data.personality,
            VisualTags = data.visualTags
        };
    }
    
    private StoryInfo.LocationProfile ConvertToLocationProfile(AILocationData data)
    {
        if (data == null)
        {
            return null;
        }
        
        return new StoryInfo.LocationProfile
        {
            Id = data.id,
            DisplayName = data.name,
            Description = data.description,
            Palette = data.palette,
            TimeOfDay = data.timeOfDay,
            Atmosphere = data.atmosphere
        };
    }
    
    private StoryInfo.StoryStyleGuide ConvertToStyleGuide(AIStyleData data)
    {
        if (data == null)
        {
            return null;
        }
        
        return new StoryInfo.StoryStyleGuide
        {
            ArtDirection = data.artDirection,
            Palette = data.palette,
            CameraPreferences = data.camera,
            Lighting = data.lighting,
            Keywords = data.keywords,
            NegativeKeywords = data.negativeKeywords
        };
    }
    
    private StoryInfo.StoryScene ConvertToStoryScene(AISceneData sceneData)
    {
        if (sceneData == null)
        {
            return null;
        }
        
        var scene = new StoryInfo.StoryScene
        {
            Title = sceneData.title,
            Description = sceneData.description,
            Setting = sceneData.setting,
            LocationId = sceneData.locationId,
            Camera = sceneData.camera,
            Lighting = sceneData.lighting,
            Mood = sceneData.mood,
            ImportantObjects = sceneData.importantObjects,
            VisualPromptNotes = sceneData.visualPrompt,
            NegativePromptNotes = sceneData.negativePrompt,
            Lines = new List<string>()
        };
        
        if (sceneData.characters != null)
        {
            scene.CharactersInScene.AddRange(sceneData.characters.Where(id => !string.IsNullOrWhiteSpace(id)));
        }
        
        if (sceneData.additionalLocations != null)
        {
            scene.AdditionalLocationIds.AddRange(sceneData.additionalLocations.Where(id => !string.IsNullOrWhiteSpace(id)));
        }
        
        AddStoryTextLines(scene.Lines, scene.Description);
        
        if (sceneData.dialogues != null && sceneData.dialogues.Length > 0)
        {
            foreach (var dialogue in sceneData.dialogues)
            {
                if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.text))
                {
                    continue;
                }
                
                var dialogueLine = new StoryInfo.StoryDialogueLine
                {
                    Speaker = dialogue.speaker,
                    Text = dialogue.text
                };
                scene.Dialogues.Add(dialogueLine);
                
                AddDialogueLines(scene.Lines, dialogue.speaker, dialogue.text);
            }
        }
        
        NormalizeStorySceneLines(scene);
        
        if (scene.Lines.Count == 0)
        {
            scene.Lines.Add($"[场景 {sceneData.index}]");
        }
        
        return scene;
    }
    
    private void AddStoryTextLines(List<string> target, string text)
    {
        if (target == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        
        target.Add(text.Trim());
    }
    
    private void AddDialogueLines(List<string> target, string speaker, string text)
    {
        if (target == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        
        if (string.IsNullOrWhiteSpace(speaker))
        {
            AddStoryTextLines(target, text);
            return;
        }
        
        string trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            return;
        }
        
        target.Add($"{speaker}: {trimmedText}");
    }
    
    private IEnumerable<string> SplitTextByLength(string text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text) || maxCharacters <= 0)
        {
            yield break;
        }
        
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int index = 0;
        int length = normalized.Length;
        
        while (index < length)
        {
            while (index < length && char.IsWhiteSpace(normalized[index]))
            {
                index++;
            }
            
            if (index >= length)
            {
                yield break;
            }
            
            int remaining = length - index;
            if (remaining <= maxCharacters)
            {
                string tail = normalized.Substring(index, remaining).Trim();
                if (!string.IsNullOrEmpty(tail))
                {
                    yield return tail;
                }
                yield break;
            }
            
            int hardLimit = Math.Min(index + maxCharacters, length);
            int split = hardLimit;
            for (int i = Math.Min(hardLimit - 1, length - 1); i >= index; i--)
            {
                if (IsSplitCharacter(normalized[i]))
                {
                    split = i + 1;
                    break;
                }
            }
            
            if (split <= index)
            {
                split = hardLimit;
            }
            
            string chunk = normalized.Substring(index, split - index).Trim();
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
            
            index = split;
        }
    }
    
    private bool IsSplitCharacter(char c)
    {
        return Array.IndexOf(StoryTextSplitCandidates, c) >= 0;
    }
    
    private void NormalizeStorySceneLines(StoryInfo.StoryScene scene)
    {
        if (scene == null || scene.Lines == null || scene.Lines.Count == 0)
        {
            return;
        }
        
        var normalized = new List<string>();
        foreach (var line in scene.Lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            
            foreach (var segment in SplitTextByLength(line, StoryTextCharactersPerPageLimit))
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    normalized.Add(segment);
                }
            }
        }
        
        scene.Lines.Clear();
        if (normalized.Count == 0)
        {
            scene.Lines.Add("[空白]");
        }
        else
        {
            scene.Lines.AddRange(normalized);
        }
    }
    
    public static void LogStoryContentForDebug(StoryInfo storyInfo)
    {
        if (storyInfo?.StoryScenes == null || storyInfo.StoryScenes.Count == 0)
        {
            Debug.Log("[AI Story] StoryInfo is empty, nothing to log.");
            return;
        }
        
        var builder = new StringBuilder();
        builder.AppendLine("[AI Story] Full story content:");
        
        for (int i = 0; i < storyInfo.StoryScenes.Count; i++)
        {
            var scene = storyInfo.StoryScenes[i];
            if (scene == null)
            {
                builder.AppendLine($"Scene {i + 1}: [null]");
                continue;
            }
            
            builder.AppendLine($"Scene {i + 1}: {scene.Title}");
            if (scene.Lines != null && scene.Lines.Count > 0)
            {
                for (int j = 0; j < scene.Lines.Count; j++)
                {
                    builder.AppendLine($"  Line {j + 1}: {scene.Lines[j]}");
                }
            }
            else
            {
                builder.AppendLine("  (No lines)");
            }
        }
        
        Debug.Log(builder.ToString());
    }
    
    private List<StoryInfo.StoryScene> EnsureSceneCount(List<StoryInfo.StoryScene> scenes, int expectedCount)
    {
        if (scenes == null)
        {
            scenes = new List<StoryInfo.StoryScene>();
        }
        
        if (scenes.Count == expectedCount)
        {
            return scenes;
        }
        
        if (scenes.Count > expectedCount)
        {
            Debug.LogWarning($"[AI Story Parse] Trimming {scenes.Count} → {expectedCount}");
            return scenes.GetRange(0, expectedCount);
        }
        
        Debug.LogWarning($"[AI Story Parse] Padding {scenes.Count} → {expectedCount}");
        while (scenes.Count < expectedCount)
        {
            scenes.Add(CreatePlaceholderScene(scenes.Count + 1));
        }
        
        return scenes;
    }
    
    private List<StoryInfo.StoryScene> CreatePlaceholderScenes(int count)
    {
        var result = new List<StoryInfo.StoryScene>();
        for (int i = 1; i <= count; i++)
        {
            result.Add(CreatePlaceholderScene(i));
        }
        return result;
    }
    
    private StoryInfo.StoryScene CreatePlaceholderScene(int index)
    {
        return new StoryInfo.StoryScene
        {
            Title = $"场景 {index}",
            Description = $"[自动填充场景 {index}]",
            Lines = new List<string> { $"[场景 {index}]" }
        };
    }
    
    private List<StoryInfo.StoryScene> FallbackParseStoryText(string storyText)
    {
        var scenes = new List<StoryInfo.StoryScene>();
        var lines = storyText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var currentScene = new StoryInfo.StoryScene
        {
            Title = "Fallback Scene",
            Lines = new List<string>()
        };
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (string.IsNullOrEmpty(trimmedLine) ||
                trimmedLine.StartsWith("{") ||
                trimmedLine.StartsWith("}") ||
                trimmedLine.StartsWith("[") ||
                trimmedLine.StartsWith("]") ||
                trimmedLine.Contains("\"scenes\""))
            {
                continue;
            }
            
            trimmedLine = trimmedLine.Trim('"', ',', ' ');
            
            AddStoryTextLines(currentScene.Lines, trimmedLine);
        }
        
        NormalizeStorySceneLines(currentScene);
        
        if (currentScene.Lines.Count > 0)
        {
            currentScene.Description = currentScene.Lines[0];
            scenes.Add(currentScene);
        }
        else
        {
            currentScene.Lines.Add("[场景 1]");
            currentScene.Description = currentScene.Lines[0];
            scenes.Add(currentScene);
        }
        
        return scenes;
    }
    
    /// <summary>
    /// 清理JSON文本，移除markdown代码块标记和其他干扰字符
    /// </summary>
    private string CleanJsonFromMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        text = text.Trim();
        
        if (text.StartsWith("```json"))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("```"))
        {
            text = text.Substring(3);
        }
        
        if (text.EndsWith("```"))
        {
            text = text.Substring(0, text.Length - 3);
        }
        
        text = text.Trim();
        
        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            text = text.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        
        return text;
    }
    
    /// <summary>
    /// 为故事场景生成图片
    /// </summary>
    private async Task GenerateStoryImagesAsync(List<StoryInfo.StoryScene> scenes)
    {
        // 获取配置的图片宽高比
        string aspectRatio = serviceConfig?.ImageAspectRatio ?? "16:9";
        
        for (int i = 0; i < scenes.Count; i++)
        {
            try
            {
                // Give battle loop a frame before heavy image work.
                await UniTask.Yield(PlayerLoopTiming.Update);

                var scene = scenes[i];
                Debug.Log($"[AI Story Image] Generating image for scene {i + 1}/{scenes.Count}...");
                
                var imagePrompt = BuildImagePrompt(scene, i, scenes.Count);
                string imageCacheKey = null;
                if (currentClient is GeminiClient && !string.IsNullOrWhiteSpace(currentStoryCacheKey))
                {
                    var imageModel = serviceConfig?.GeminiConfig?.ImageModel;
                    imageCacheKey = BuildImageCacheKey(currentStoryCacheKey, i + 1, imagePrompt, aspectRatio, imageModel);
                }
                
                // 使用AI生成图片（支持缓存）
                Texture2D[] textures;
                if (currentClient is GeminiClient geminiClient && !string.IsNullOrWhiteSpace(imageCacheKey))
                {
                    textures = await geminiClient.GeneratePicWithCache(imagePrompt, imageCacheKey, currentStoryCacheKey, i + 1, 1, aspectRatio);
                }
                else
                {
                    textures = await GeneratePic(imagePrompt, 1, aspectRatio);
                }
                
                if (textures != null && textures.Length > 0 && textures[0] != null)
                {
                    var texture = textures[0];
                    if (texture.width > 0 && texture.height > 0)
                    {
                        // 将Texture2D转换为Sprite
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        scene.Pic = sprite;
                        Debug.Log($"[AI Story Image] ✓ Scene {i + 1} image generated successfully ({texture.width}x{texture.height})");
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Story Image] Scene {i + 1} image has invalid dimensions");
                    }
                }
                else
                {
                    Debug.LogWarning($"[AI Story Image] Failed to generate image for scene {i + 1}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Story Image] Error generating image for scene {i + 1}: {ex.Message}");
            }

            // Hand control back to the game loop between scenes to minimize hitches.
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
    
    /// <summary>
    /// 构建场景的描述内容，用于图片生成
    /// </summary>
    private string BuildStoryOverview(StoryInfo.StoryScene scene)
    {
        if (scene == null)
        {
            return string.Empty;
        }
        
        var builder = new StringBuilder();
        
        if (!string.IsNullOrEmpty(scene.Description))
        {
            builder.Append(scene.Description);
        }
        else if (scene.Lines != null && scene.Lines.Count > 0)
        {
            builder.Append(scene.Lines[0]);
        }
        
        if (scene.Dialogues != null && scene.Dialogues.Count > 0)
        {
            foreach (var dialogue in scene.Dialogues)
            {
                if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.Text))
                {
                    continue;
                }
                
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                
                if (!string.IsNullOrWhiteSpace(dialogue.Speaker))
                {
                    builder.Append($"{dialogue.Speaker}: ");
                }
                
                builder.Append(dialogue.Text);
            }
        }
        else if (scene.Lines != null && scene.Lines.Count > 1)
        {
            for (int i = 1; i < scene.Lines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(scene.Lines[i]))
                {
                    builder.Append(' ').Append(scene.Lines[i]);
                }
            }
        }
        
        return builder.ToString().Trim();
    }
    
    /// <summary>
    /// 构建图片生成的提示词
    /// </summary>
    private string BuildImagePrompt(StoryInfo.StoryScene scene, int sceneIndex, int totalScenes)
    {
        string sceneContent = BuildStoryOverview(scene);
        
        const int maxPromptLength = 500;
        if (sceneContent.Length > maxPromptLength)
        {
            sceneContent = sceneContent.Substring(0, maxPromptLength) + "...";
            Debug.Log($"[AI Story Image] Scene content truncated to {maxPromptLength} characters");
        }
        
        var builder = new StringBuilder();
        builder.Append($"Scene {sceneIndex + 1}/{totalScenes}. ");
        
        if (!string.IsNullOrEmpty(scene?.Title))
        {
            builder.Append($"{scene.Title}. ");
        }
        
        if (!string.IsNullOrEmpty(sceneContent))
        {
            builder.Append(sceneContent).Append(' ');
        }
        
        var characterPrompt = BuildCharacterPrompt(scene?.CharactersInScene);
        if (!string.IsNullOrEmpty(characterPrompt))
        {
            builder.Append(characterPrompt).Append(' ');
        }
        
        var locationPrompt = BuildLocationPrompt(scene);
        if (!string.IsNullOrEmpty(locationPrompt))
        {
            builder.Append(locationPrompt).Append(' ');
        }
        
        if (!string.IsNullOrEmpty(scene?.ImportantObjects))
        {
            builder.Append($"Key props: {scene.ImportantObjects}. ");
        }
        
        if (!string.IsNullOrEmpty(scene?.VisualPromptNotes))
        {
            builder.Append(scene.VisualPromptNotes).Append(". ");
        }
        
        var stylePrompt = BuildStylePrompt(scene);
        if (!string.IsNullOrEmpty(stylePrompt))
        {
            builder.Append(stylePrompt).Append(' ');
        }
        
        var prompt = builder.ToString().Trim();
        
        var negativeTokens = new List<string>(DefaultImageNegativeTokens);
        if (!string.IsNullOrWhiteSpace(scene?.NegativePromptNotes))
        {
            negativeTokens.Add(scene.NegativePromptNotes);
        }
        if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.NegativeKeywords))
        {
            negativeTokens.Add(currentStoryStyleGuide.NegativeKeywords);
        }

        var filteredNegativeTokens = negativeTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filteredNegativeTokens.Count > 0)
        {
            prompt += $" Avoid: {string.Join(", ", filteredNegativeTokens)}.";
        }
        
        scene.BuiltPrompt = prompt;
        Debug.Log($"[AI Story Image] Prompt for scene {sceneIndex + 1}: {prompt}");
        
        return prompt;
    }
    
    private string BuildCharacterPrompt(IEnumerable<string> characterIds)
    {
        if (characterIds == null || currentStoryCharacters == null || currentStoryCharacters.Count == 0)
        {
            return string.Empty;
        }
        
        var entries = new List<string>();
        foreach (var id in characterIds)
        {
            var profile = FindCharacterProfile(id);
            if (profile == null)
            {
                continue;
            }
            
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(profile.Description))
            {
                parts.Add(profile.Description);
            }
            if (!string.IsNullOrWhiteSpace(profile.Outfit))
            {
                parts.Add(profile.Outfit);
            }
            if (!string.IsNullOrWhiteSpace(profile.VisualTags))
            {
                parts.Add(profile.VisualTags);
            }
            if (!string.IsNullOrWhiteSpace(profile.Personality))
            {
                parts.Add($"personality {profile.Personality}");
            }
            
            var name = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName;
            entries.Add(parts.Count > 0 ? $"{name}: {string.Join(", ", parts)}" : name);
        }
        
        return entries.Count > 0 ? $"Characters: {string.Join("; ", entries)}." : string.Empty;
    }
    
    private StoryInfo.CharacterProfile FindCharacterProfile(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || currentStoryCharacters == null)
        {
            return null;
        }
        
        return currentStoryCharacters.FirstOrDefault(profile =>
            string.Equals(profile.Id, reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(profile.DisplayName, reference, StringComparison.OrdinalIgnoreCase));
    }
    
    private string BuildLocationPrompt(StoryInfo.StoryScene scene)
    {
        if (scene == null)
        {
            return string.Empty;
        }
        
        var descriptions = new List<string>();
        var handledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        if (!string.IsNullOrWhiteSpace(scene.LocationId))
        {
            handledIds.Add(scene.LocationId);
            var profile = FindLocationProfile(scene.LocationId);
            if (profile != null)
            {
                descriptions.Add(DescribeLocationProfile(profile));
            }
        }
        
        if (scene.AdditionalLocationIds != null)
        {
            foreach (var locationId in scene.AdditionalLocationIds)
            {
                if (string.IsNullOrWhiteSpace(locationId) || !handledIds.Add(locationId))
                {
                    continue;
                }
                
                var profile = FindLocationProfile(locationId);
                if (profile != null)
                {
                    descriptions.Add(DescribeLocationProfile(profile));
                }
            }
        }
        
        if (!string.IsNullOrWhiteSpace(scene.Setting))
        {
            descriptions.Add(scene.Setting);
        }
        
        return descriptions.Count > 0 ? $"Environment: {string.Join(". ", descriptions)}." : string.Empty;
    }
    
    private StoryInfo.LocationProfile FindLocationProfile(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || currentStoryLocations == null)
        {
            return null;
        }
        
        return currentStoryLocations.FirstOrDefault(profile =>
            string.Equals(profile.Id, reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(profile.DisplayName, reference, StringComparison.OrdinalIgnoreCase));
    }
    
    private string DescribeLocationProfile(StoryInfo.LocationProfile profile)
    {
        if (profile == null)
        {
            return string.Empty;
        }
        
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            parts.Add(profile.DisplayName);
        }
        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            parts.Add(profile.Description);
        }
        if (!string.IsNullOrWhiteSpace(profile.Palette))
        {
            parts.Add($"palette {profile.Palette}");
        }
        if (!string.IsNullOrWhiteSpace(profile.TimeOfDay))
        {
            parts.Add(profile.TimeOfDay);
        }
        if (!string.IsNullOrWhiteSpace(profile.Atmosphere))
        {
            parts.Add(profile.Atmosphere);
        }
        
        return string.Join(", ", parts);
    }
    
    private string BuildStylePrompt(StoryInfo.StoryScene scene)
    {
        var segments = new List<string>();

        var configuredImageStyle = GetImageStyleFromConfig();
        if (!string.IsNullOrWhiteSpace(configuredImageStyle))
        {
            segments.Add(configuredImageStyle);
        }
        
        if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.ArtDirection))
        {
            segments.Add(currentStoryStyleGuide.ArtDirection);
        }
        
        if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.Palette))
        {
            segments.Add($"Palette {currentStoryStyleGuide.Palette}");
        }
        
        if (!string.IsNullOrWhiteSpace(scene?.Camera))
        {
            segments.Add($"Shot: {scene.Camera}");
        }
        else if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.CameraPreferences))
        {
            segments.Add($"Camera: {currentStoryStyleGuide.CameraPreferences}");
        }
        
        if (!string.IsNullOrWhiteSpace(scene?.Lighting))
        {
            segments.Add($"Lighting: {scene.Lighting}");
        }
        else if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.Lighting))
        {
            segments.Add($"Lighting: {currentStoryStyleGuide.Lighting}");
        }
        
        if (!string.IsNullOrWhiteSpace(scene?.Mood))
        {
            segments.Add($"Mood: {scene.Mood}");
        }
        
        if (!string.IsNullOrWhiteSpace(currentStoryStyleGuide?.Keywords))
        {
            segments.Add(currentStoryStyleGuide.Keywords);
        }
        
        return segments.Count > 0 ? string.Join(" ", segments) : string.Empty;
    }

    private async Task<string> AskStoryWithCacheAsync(string prompt, string cacheKey, int? timeoutMs = null)
    {
        if (currentClient is GeminiClient geminiClient && !string.IsNullOrWhiteSpace(cacheKey))
        {
            return await geminiClient.AskWithCacheAsync(prompt, cacheKey, timeoutMs);
        }

        return await AskAsync(prompt, timeoutMs);
    }

    private string BuildStoryCacheKey(string customPrompt)
    {
        var cacheContext = AIStoryRuntimeContext.GetCacheContext();
        var fightId = cacheContext.FightId;
        var eventType = cacheContext.EventType;
        var fightMode = cacheContext.FightMode;
        var language = GetConfiguredLanguage();
        var pageCount = serviceConfig?.PageCount ?? 0;
        var storyStyle = serviceConfig != null ? serviceConfig.StoryStyle.ToString() : "unknown";
        var customStoryStyle = serviceConfig?.CustomStoryStylePrompt ?? string.Empty;
        var extraStory = serviceConfig?.AdditionalStoryRequirements ?? string.Empty;
        var imageStyle = serviceConfig != null ? serviceConfig.ImageStyle.ToString() : "unknown";
        var customImageStyle = serviceConfig?.CustomImageStylePrompt ?? string.Empty;
        var aspectRatio = serviceConfig?.ImageAspectRatio ?? "unknown";
        var extraStyle = serviceConfig?.AdditionalImageRequirements ?? string.Empty;
        var textModel = serviceConfig?.GeminiConfig?.Model ?? string.Empty;
        var imageModel = serviceConfig?.GeminiConfig?.ImageModel ?? string.Empty;
        var themes = serviceConfig?.StoryThemes == null ? string.Empty : string.Join("|", serviceConfig.StoryThemes);
        var promptSeed = string.IsNullOrWhiteSpace(customPrompt) ? "auto" : customPrompt;
        var source = string.Join("|", new[]
        {
            StoryCacheKeyPrefix,
            fightId,
            eventType,
            fightMode,
            language.ToString(),
            pageCount.ToString(),
            storyStyle,
            customStoryStyle,
            extraStory,
            imageStyle,
            customImageStyle,
            aspectRatio,
            textModel,
            imageModel,
            extraStyle,
            themes,
            promptSeed
        });

        return $"story_{ComputeSha256Hex(source)}";
    }

    private string BuildImageCacheKey(string storyCacheKey, int sceneIndex, string prompt, string aspectRatio, string imageModel)
    {
        if (string.IsNullOrWhiteSpace(storyCacheKey) || string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var source = string.Join("|", new[]
        {
            storyCacheKey,
            sceneIndex.ToString(),
            aspectRatio ?? string.Empty,
            imageModel ?? string.Empty,
            prompt
        });

        return $"img_{ComputeSha256Hex(source)}";
    }

    private static string ComputeSha256Hex(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        using (var sha = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }
    }
    
    private string BuildLanguageInstruction(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Chinese:
                return "请使用中文编写所有标题、描述、对话和故事文本。";
            case SystemLanguage.Japanese:
                return "すべてのタイトル・説明・セリフ・本文は日本語で出力してください。";
            case SystemLanguage.English:
                return "Please write all titles, descriptions, dialogue, and narrative text in English.";
            default:
                return $"Please write all titles, descriptions, dialogue, and narrative text in {language}.";
        }
    }
    
    private SystemLanguage GetConfiguredLanguage()
    {
        var language = AIStoryRuntimeContext.GetLanguage();
        switch (language)
        {
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return SystemLanguage.Chinese;
            case SystemLanguage.Unknown:
                return SystemLanguage.English;
            default:
                return language;
        }
    }
    
    private string BuildProverbPrompt(SystemLanguage language, IReadOnlyList<string> usedProverbs)
    {
        var avoidList = BuildProverbAvoidList(usedProverbs);
        switch (language)
        {
            case SystemLanguage.Chinese:
                return string.IsNullOrEmpty(avoidList)
                    ? "请只返回一句不超过35个字的中文谚语，不要添加引号、翻译或解释。"
                    : $"请只返回一句不超过35个字的中文谚语，不要添加引号、翻译或解释。请避免与以下内容重复：{avoidList}";
            case SystemLanguage.Japanese:
                return string.IsNullOrEmpty(avoidList)
                    ? "短いことわざを日本語で1つだけ（35文字以内）返してください。引用符や説明は禁止です。"
                    : $"短いことわざを日本語で1つだけ（35文字以内）返してください。引用符や説明は禁止です。以下と重複しないでください：{avoidList}";
            case SystemLanguage.English:
                return string.IsNullOrEmpty(avoidList)
                    ? "Return exactly one concise English proverb under 50 words. No quotes, translation, or explanations."
                    : $"Return exactly one concise English proverb under 50 words. No quotes, translation, or explanations. Do not repeat any of these: {avoidList}";
            default:
                return string.IsNullOrEmpty(avoidList)
                    ? $"Return one short proverb in {language} (max 50 words). No quotes or explanations."
                    : $"Return one short proverb in {language} (max 50 words). No quotes or explanations. Do not repeat any of these: {avoidList}";
        }
    }

    private static List<string> GetProverbHistorySnapshot(SystemLanguage language)
    {
        lock (ProverbHistoryLock)
        {
            if (ProverbHistoryLanguage != language)
            {
                ProverbHistoryLanguage = language;
                ProverbHistory.Clear();
                ProverbHistoryKeys.Clear();
            }

            return ProverbHistory.Count == 0 ? new List<string>() : new List<string>(ProverbHistory);
        }
    }

    private static void RecordProverb(SystemLanguage language, string proverb)
    {
        var normalized = NormalizeProverb(proverb);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        lock (ProverbHistoryLock)
        {
            if (ProverbHistoryLanguage != language || ProverbHistoryKeys.Contains(normalized))
            {
                return;
            }

            ProverbHistoryKeys.Add(normalized);
            ProverbHistory.Add(proverb.Trim());
        }
    }

    private static string NormalizeProverb(string proverb)
    {
        if (string.IsNullOrWhiteSpace(proverb))
        {
            return null;
        }

        var parts = proverb.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(" ", parts);
    }

    private static string BuildProverbAvoidList(IReadOnlyList<string> usedProverbs)
    {
        if (usedProverbs == null || usedProverbs.Count == 0)
        {
            return null;
        }

        return string.Join("; ", usedProverbs);
    }
    
    private string ExtractSingleLineText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }
        
        var firstLine = rawText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }
        
        var cleaned = firstLine.Trim().Trim('\"', '“', '”', '\'');
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private bool IsAIStoryComplete(List<StoryInfo.StoryScene> scenes)
    {
        if (scenes == null || scenes.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < scenes.Count; i++)
        {
            if (!IsStorySceneComplete(scenes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsStorySceneComplete(StoryInfo.StoryScene scene)
    {
        if (scene == null || scene.Pic == null)
        {
            return false;
        }

        if (scene.Lines == null || scene.Lines.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < scene.Lines.Count; i++)
        {
            if (IsMeaningfulStoryLine(scene.Lines[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMeaningfulStoryLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed == "[空白]" ||
            trimmed.StartsWith("[场景", StringComparison.Ordinal) ||
            trimmed.StartsWith("[自动填充场景", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
    
    private class ParsedStoryResult
    {
        public List<StoryInfo.StoryScene> Scenes;
        public List<StoryInfo.CharacterProfile> Characters;
        public List<StoryInfo.LocationProfile> Locations;
        public StoryInfo.StoryStyleGuide StyleGuide;
    }
    
    /// <summary>
    /// AI故事数据解析类
    /// </summary>
    [Serializable]
    private class AIStoryData
    {
        public AICharacterData[] characters;
        public AILocationData[] locations;
        public AIStyleData style;
        public AISceneData[] scenes;
    }
    
    [Serializable]
    private class AICharacterData
    {
        public string id;
        public string name;
        public string appearance;
        public string outfit;
        public string personality;
        public string visualTags;
    }
    
    [Serializable]
    private class AILocationData
    {
        public string id;
        public string name;
        public string description;
        public string palette;
        public string timeOfDay;
        public string atmosphere;
    }
    
    [Serializable]
    private class AIStyleData
    {
        public string artDirection;
        public string palette;
        public string camera;
        public string lighting;
        public string keywords;
        public string negativeKeywords;
    }
    
    [Serializable]
    private class AISceneData
    {
        public int index;
        public string title;
        public string description;
        public string setting;
        public string locationId;
        public string[] additionalLocations;
        public string[] characters;
        public string mood;
        public string importantObjects;
        public string camera;
        public string lighting;
        public string visualPrompt;
        public string negativePrompt;
        public AISceneDialogue[] dialogues;
    }
    
    [Serializable]
    private class AISceneDialogue
    {
        public string speaker;
        public string text;
    }
    
    /// <summary>
    /// 根据配置获取故事主题描述（从主题列表中随机选择）
    /// </summary>
    private string GetStoryThemeFromConfig()
    {
        var availableThemes = serviceConfig?.StoryThemes?
            .Where(theme => !string.IsNullOrWhiteSpace(theme))
            .ToArray();
        
        if (availableThemes == null || availableThemes.Length == 0)
        {
            string fallbackTheme = BuildFallbackStoryTheme();
            Debug.Log($"[AIServiceManager] Selected fallback story theme: {fallbackTheme}");
            return fallbackTheme;
        }
        
        // 从主题列表中随机选择一个
        var random = new System.Random();
        string selectedTheme = availableThemes[random.Next(availableThemes.Length)];
        
        Debug.Log($"[AIServiceManager] Selected story theme: {selectedTheme}");
        return selectedTheme;
    }
    
    private StoryFallbackConfigBase GetFallbackThemeConfig()
    {
        if (fallbackThemeConfig != null)
        {
            return fallbackThemeConfig;
        }

        return CreateDefaultFallbackConfig(GetFallbackTone());
    }
    
    private string BuildFallbackStoryTheme()
    {
        var random = new System.Random();
        int pageCount = Math.Max(serviceConfig?.PageCount ?? 6, 1);
        
        var fallbackConfig = GetFallbackThemeConfig();
        
        string setting = PickRandom(fallbackConfig.Settings, random);
        string worldDetail = PickRandom(fallbackConfig.WorldDetails, random);
        string hero = PickRandom(fallbackConfig.Heroes, random);
        string companion = PickRandom(fallbackConfig.Companions, random);
        string goal = PickRandom(fallbackConfig.Goals, random);
        string conflict = PickRandom(fallbackConfig.Conflicts, random);
        string resolution = PickRandom(fallbackConfig.Resolutions, random);

        string theme = $"在{setting}背景中，{worldDetail}。故事讲述{hero}{companion}，他们需要{goal}，途中{conflict}，最终{resolution}。";
        string styleGuidance = FormatStyleGuidance(fallbackConfig.StyleGuidance, pageCount);

        if (!string.IsNullOrWhiteSpace(styleGuidance))
        {
            theme = $"{theme}{styleGuidance}";
        }

        return theme;
    }

    private string FormatStyleGuidance(string styleGuidance, int pageCount)
    {
        if (string.IsNullOrWhiteSpace(styleGuidance))
        {
            return string.Empty;
        }

        return styleGuidance.Replace("{pageCount}", pageCount.ToString());
    }

    private StoryFallbackTone GetFallbackTone()
    {
        return serviceConfig?.FallbackTone ?? StoryFallbackTone.FairyTale;
    }

    private string ResolveFallbackConfigAddress(StoryFallbackTone tone)
    {
        if (serviceConfig != null)
        {
            var addressFromConfig = serviceConfig.GetSelectedFallbackAddress();
            if (!string.IsNullOrWhiteSpace(addressFromConfig))
            {
                return addressFromConfig;
            }

            // When AIServiceConfig is present but empty, prefer built-in defaults for the chosen tone.
            return null;
        }

        // Backward compatibility: fall back to serialized field if config is empty
        return fallbackThemeConfigAddress;
    }

    private StoryFallbackConfigBase MergeFallbackConfig(StoryFallbackConfigBase source, StoryFallbackTone tone)
    {
        if (source == null)
        {
            return CreateDefaultFallbackConfig(tone);
        }

        if (source is FairyTaleFallbackConfig fairy)
        {
            return FairyTaleFallbackConfig.CreateMerged(fairy);
        }

        if (source is MatureStoryFallbackConfig mature)
        {
            return MatureStoryFallbackConfig.CreateMerged(mature);
        }

        // Unknown derived type: try to merge with its own defaults
        return source.CreateMergedInstance() ?? CreateDefaultFallbackConfig(tone);
    }

    private StoryFallbackConfigBase CreateDefaultFallbackConfig(StoryFallbackTone tone)
    {
        return tone switch
        {
            StoryFallbackTone.Mature => StoryFallbackConfigBase.CreateDefault<MatureStoryFallbackConfig>(),
            _ => StoryFallbackConfigBase.CreateDefault<FairyTaleFallbackConfig>()
        };
    }
    
    private string PickRandom(string[] source, System.Random random)
    {
        if (source == null)
        {
            return string.Empty;
        }
        
        var candidates = source.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (candidates.Length == 0)
        {
            return string.Empty;
        }
        
        return candidates[random.Next(candidates.Length)];
    }
    
    /// <summary>
    /// 根据配置获取图片风格描述
    /// </summary>
    private string GetImageStyleFromConfig()
    {
        if (serviceConfig == null)
        {
            return "photorealistic style, high quality, natural colors";
        }

        return serviceConfig.GetImageStylePrompt();
    }
}

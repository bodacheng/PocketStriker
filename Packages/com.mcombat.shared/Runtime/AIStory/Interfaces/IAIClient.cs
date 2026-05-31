using System.Threading.Tasks;

/// <summary>
/// Abstract interface for AI clients to enable model switching
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Send a text prompt and get AI response
    /// </summary>
    /// <param name="question">The prompt/question to send</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds</param>
    /// <returns>AI response text</returns>
    Task<string> AskAsync(string question, int? timeoutMs = null);
    
    /// <summary>
    /// Generate images from a text prompt
    /// </summary>
    /// <param name="prompt">Image generation prompt</param>
    /// <param name="count">Number of images to generate</param>
    /// <param name="aspectRatio">Aspect ratio for the images</param>
    /// <returns>Array of generated textures</returns>
    Task<UnityEngine.Texture2D[]> GeneratePic(string prompt, int? count = null, string aspectRatio = null);
    
    /// <summary>
    /// Get the name of the current AI provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Check if the client is properly configured
    /// </summary>
    bool IsConfigured { get; }
}


using System;
using UnityEngine;

public readonly struct AIStoryCacheContext
{
    public readonly string FightId;
    public readonly string EventType;
    public readonly string FightMode;

    public AIStoryCacheContext(string fightId, string eventType, string fightMode)
    {
        FightId = string.IsNullOrWhiteSpace(fightId) ? "unknown" : fightId;
        EventType = string.IsNullOrWhiteSpace(eventType) ? "none" : eventType;
        FightMode = string.IsNullOrWhiteSpace(fightMode) ? "none" : fightMode;
    }

    public static AIStoryCacheContext Empty => new AIStoryCacheContext("unknown", "none", "none");
}

public static class AIStoryRuntimeContext
{
    public static Func<SystemLanguage> LanguageProvider { get; set; }
    public static Func<AIStoryCacheContext> CacheContextProvider { get; set; }

    public static SystemLanguage GetLanguage()
    {
        if (LanguageProvider == null)
        {
            return Application.systemLanguage;
        }

        try
        {
            return LanguageProvider();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIStoryRuntimeContext] Language provider failed: {ex.Message}");
            return Application.systemLanguage;
        }
    }

    public static AIStoryCacheContext GetCacheContext()
    {
        if (CacheContextProvider == null)
        {
            return AIStoryCacheContext.Empty;
        }

        try
        {
            return CacheContextProvider();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIStoryRuntimeContext] Cache context provider failed: {ex.Message}");
            return AIStoryCacheContext.Empty;
        }
    }
}

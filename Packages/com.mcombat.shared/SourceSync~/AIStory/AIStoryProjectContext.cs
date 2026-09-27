using UnityEngine;

public static class AIStoryProjectContext
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        AIStoryRuntimeContext.LanguageProvider = () =>
            AppSetting.Value != null ? AppSetting.Value.Language : Application.systemLanguage;

        AIStoryRuntimeContext.CacheContextProvider = () =>
        {
            var fight = FightLoad.Fight;
            return fight == null
                ? AIStoryCacheContext.Empty
                : new AIStoryCacheContext(
                    fight.ID,
                    fight.EventType.ToString(),
                    fight.FightMode.ToString());
        };
    }
}

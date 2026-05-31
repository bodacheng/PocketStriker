using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base class for fallback story configs so different tones can share the same structure.
/// </summary>
public abstract class StoryFallbackConfigBase : ScriptableObject
{
    [SerializeField] private string[] settings = Array.Empty<string>();
    [SerializeField] private string[] worldDetails = Array.Empty<string>();
    [SerializeField] private string[] heroes = Array.Empty<string>();
    [SerializeField] private string[] companions = Array.Empty<string>();
    [SerializeField] private string[] goals = Array.Empty<string>();
    [SerializeField] private string[] conflicts = Array.Empty<string>();
    [SerializeField] private string[] resolutions = Array.Empty<string>();
    [SerializeField] [TextArea(2, 5)] private string styleGuidance = string.Empty;

    public string[] Settings => settings;
    public string[] WorldDetails => worldDetails;
    public string[] Heroes => heroes;
    public string[] Companions => companions;
    public string[] Goals => goals;
    public string[] Conflicts => conflicts;
    public string[] Resolutions => resolutions;
    public string StyleGuidance => styleGuidance;

    protected abstract string[] DefaultSettings { get; }
    protected abstract string[] DefaultWorldDetails { get; }
    protected abstract string[] DefaultHeroes { get; }
    protected abstract string[] DefaultCompanions { get; }
    protected abstract string[] DefaultGoals { get; }
    protected abstract string[] DefaultConflicts { get; }
    protected abstract string[] DefaultResolutions { get; }
    protected virtual string DefaultStyleGuidance => string.Empty;

    public StoryFallbackConfigBase CreateMergedInstance()
    {
        var instance = CreateInstance(GetType()) as StoryFallbackConfigBase;
        if (instance == null)
        {
            return null;
        }

        instance.settings = instance.SelectArray(settings, DefaultSettings);
        instance.worldDetails = instance.SelectArray(worldDetails, DefaultWorldDetails);
        instance.heroes = instance.SelectArray(heroes, DefaultHeroes);
        instance.companions = instance.SelectArray(companions, DefaultCompanions);
        instance.goals = instance.SelectArray(goals, DefaultGoals);
        instance.conflicts = instance.SelectArray(conflicts, DefaultConflicts);
        instance.resolutions = instance.SelectArray(resolutions, DefaultResolutions);
        instance.styleGuidance = instance.SelectValue(styleGuidance, DefaultStyleGuidance);

        return instance;
    }

    public StoryFallbackConfigBase CreateDefaultInstance()
    {
        var instance = CreateInstance(GetType()) as StoryFallbackConfigBase;
        if (instance == null)
        {
            return null;
        }

        instance.ApplyDefaultValues();
        return instance;
    }

    public static T CreateDefault<T>() where T : StoryFallbackConfigBase
    {
        var instance = CreateInstance<T>();
        instance.ApplyDefaultValues();
        return instance;
    }

    public static T CreateMerged<T>(T source) where T : StoryFallbackConfigBase
    {
        if (source == null)
        {
            return CreateDefault<T>();
        }

        var instance = CreateInstance<T>();
        instance.settings = instance.SelectArray(source.settings, source.DefaultSettings);
        instance.worldDetails = instance.SelectArray(source.worldDetails, source.DefaultWorldDetails);
        instance.heroes = instance.SelectArray(source.heroes, source.DefaultHeroes);
        instance.companions = instance.SelectArray(source.companions, source.DefaultCompanions);
        instance.goals = instance.SelectArray(source.goals, source.DefaultGoals);
        instance.conflicts = instance.SelectArray(source.conflicts, source.DefaultConflicts);
        instance.resolutions = instance.SelectArray(source.resolutions, source.DefaultResolutions);
        instance.styleGuidance = instance.SelectValue(source.styleGuidance, source.DefaultStyleGuidance);

        return instance;
    }

    protected virtual void ApplyDefaultValues()
    {
        settings = DefaultSettings?.ToArray() ?? Array.Empty<string>();
        worldDetails = DefaultWorldDetails?.ToArray() ?? Array.Empty<string>();
        heroes = DefaultHeroes?.ToArray() ?? Array.Empty<string>();
        companions = DefaultCompanions?.ToArray() ?? Array.Empty<string>();
        goals = DefaultGoals?.ToArray() ?? Array.Empty<string>();
        conflicts = DefaultConflicts?.ToArray() ?? Array.Empty<string>();
        resolutions = DefaultResolutions?.ToArray() ?? Array.Empty<string>();
        styleGuidance = DefaultStyleGuidance ?? string.Empty;
    }

    protected string[] SelectArray(string[] primary, string[] fallback)
    {
        if (primary != null)
        {
            var filtered = primary.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (filtered.Length > 0)
            {
                return filtered;
            }
        }

        return fallback != null ? fallback.ToArray() : Array.Empty<string>();
    }

    protected string SelectValue(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback ?? string.Empty : primary;
    }
}

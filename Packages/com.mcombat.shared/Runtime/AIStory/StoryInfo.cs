using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoryInfo : ScriptableObject
{
    [SerializeField] private List<StoryScene> _storyScenes;
    [SerializeField] private List<CharacterProfile> _characters;
    [SerializeField] private List<LocationProfile> _locations;
    [SerializeField] private StoryStyleGuide _styleGuide;
    
    public List<StoryScene> StoryScenes
    {
        get => _storyScenes;
        set => _storyScenes = value;
    }
    
    public List<CharacterProfile> Characters
    {
        get => _characters;
        set => _characters = value;
    }
    
    public List<LocationProfile> Locations
    {
        get => _locations;
        set => _locations = value;
    }
    
    public StoryStyleGuide StyleGuide
    {
        get => _styleGuide;
        set => _styleGuide = value;
    }

    public bool HasVisualScene()
    {
        return FindNextVisualSceneIndex(-1) >= 0;
    }

    public int FindNextVisualSceneIndex(int currentIndex)
    {
        if (_storyScenes == null)
        {
            return -1;
        }

        for (var i = currentIndex + 1; i < _storyScenes.Count; i++)
        {
            if (_storyScenes[i]?.Pic != null)
            {
                return i;
            }
        }

        return -1;
    }

    [Serializable]
    public class StoryScene
    {
        public Sprite Pic;
        public string Title;
        [TextArea] public string Description;
        [TextArea] public string Setting;
        public string LocationId;
        public List<string> AdditionalLocationIds = new List<string>();
        public List<string> CharactersInScene = new List<string>();
        public List<StoryDialogueLine> Dialogues = new List<StoryDialogueLine>();
        public string Camera;
        public string Lighting;
        public string Mood;
        public string ImportantObjects;
        [TextArea] public string VisualPromptNotes;
        [TextArea] public string NegativePromptNotes;
        [TextArea] public string BuiltPrompt;
        public List<string> Lines = new List<string>();
    }
    
    [Serializable]
    public class StoryDialogueLine
    {
        public string Speaker;
        [TextArea] public string Text;
    }
    
    [Serializable]
    public class CharacterProfile
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        [TextArea] public string Outfit;
        [TextArea] public string Personality;
        [TextArea] public string VisualTags;
    }
    
    [Serializable]
    public class LocationProfile
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        [TextArea] public string Palette;
        [TextArea] public string TimeOfDay;
        [TextArea] public string Atmosphere;
    }
    
    [Serializable]
    public class StoryStyleGuide
    {
        [TextArea] public string ArtDirection;
        [TextArea] public string Palette;
        [TextArea] public string CameraPreferences;
        [TextArea] public string Lighting;
        [TextArea] public string Keywords;
        [TextArea] public string NegativeKeywords;
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetTeam"></param>
    /// <param name="path">"Assets/" 开头</param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static StoryInfo CreateStoryAsset(string path, string fileName)
    {
        var StoryInfo = CreateInstance<StoryInfo>();
        if (!Directory.Exists(path))
        {
            //if it doesn't, create it
            Directory.CreateDirectory(path);
        }
        
        AssetDatabase.CreateAsset(StoryInfo, path + "/" + fileName + ".asset");
        Debug.Log("Generated：" + path + "/" + fileName + ".asset");
        AssetDatabase.Refresh();
        return StoryInfo;
    }
#endif
}

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AudioResourceLoading
{
    private static AudioResourceLoading instance;
    public static AudioResourceLoading Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AudioResourceLoading();
            }
            return instance;
        }
    }
    public IDictionary<string, AudioClip> SoundClipsDic => AudioResourceLoaderCore.SoundClipsDic;
    
    public async UniTask LoadAudioClipFromResourceAndPutItIntoDic(string additionalPath, string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        var path = AudioResourceLoaderCore.AudioClipKey(additionalPath, clipName);
        if (AudioResourceLoaderCore.HasAudioClip(path))
            return;

        if (AddressablesLogic.HasIndexedTag(AudioResourceLoaderCore.AudioLabel) &&
            !AddressablesLogic.CheckKeyExist(AudioResourceLoaderCore.AudioLabel, path))
        {
            Debug.LogWarning($"[AudioResourceLoading] Missing audio addressable key: {path}");
            return;
        }

        var audioClip = await AddressablesLogic.LoadT<AudioClip>(path);
        if (audioClip == null)
        {
            Debug.LogWarning($"[AudioResourceLoading] Failed to load audio clip: {path}");
            return;
        }

        AudioResourceLoaderCore.AddOrReplaceAudioClip(path, audioClip);
    }
}

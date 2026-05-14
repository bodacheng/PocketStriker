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
    
    public UniTask LoadAudioClipFromResourceAndPutItIntoDic(string additionalPath, string clipName) =>
        AudioResourceLoadingCore.LoadAudioClipIntoCache(
            additionalPath,
            clipName,
            AddressablesLogic.HasIndexedTag,
            AddressablesLogic.CheckKeyExist,
            key => AddressablesLogic.LoadT<AudioClip>(key),
            Debug.LogWarning);
}

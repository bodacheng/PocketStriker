using System.Collections;
using System.Collections.Generic;
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
    public IDictionary<string, AudioClip> soundClipsDic = new Dictionary<string, AudioClip>();
    
    public IEnumerator LoadAudioClipFromCachAndPutItIntoDic(string bundleURL,string additionalPath, string clip_name)
    {
        // AudioClip _AudioClip = null;
        // string clipkey = additionalPath + "/" + clip_name;
        // soundClipsDic.TryGetValue(clipkey, out _AudioClip);
        // if (_AudioClip != null)
        // {
        //     Debug.Log("声音：" + clipkey + "已经存在");
        //     yield break;
        // }
        // IEnumerator task = CachManager.Instance.getABFromCach(bundleURL,clip_name);
        // yield return task;
        // AssetBundle readingBundle = (AssetBundle)task.Current;
        // if (readingBundle != null)
        // {
        //     _AudioClip = readingBundle.LoadAsset<AudioClip>(clip_name);
        //     if (_AudioClip != null)
        //     {
        //         readingBundle.Unload(false);
        //         if (!soundClipsDic.ContainsKey(clipkey))
        //             soundClipsDic.Add(clipkey, _AudioClip);
        //         else
        //             soundClipsDic[clipkey] = _AudioClip;
        //     }
        //     else
        //     {
        //         readingBundle.Unload(false);
        //         yield break;
        //     }
        // }
        
        yield break;
    }
    
    public void LoadAudioClipFromResourceAndPutItIntoDic(string additionalPath, string clip_name)
    {
        string clipkey = "Audios/" + additionalPath + "/" + clip_name;
        AudioClip audioClip = Resources.Load(clipkey, typeof(AudioClip)) as AudioClip;
        if (soundClipsDic.ContainsKey(clipkey))
            soundClipsDic[clipkey] = audioClip;
        else
            soundClipsDic.Add(clipkey, audioClip);
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public partial class AnimationManger
{
    private static readonly IDictionary<string, UniTaskCompletionSource<List<AnimationClip>>> PendingSeriesLoads =
        new Dictionary<string, UniTaskCompletionSource<List<AnimationClip>>>();
    private static readonly IDictionary<string, List<string>> AnimationLocationKeys =
        new Dictionary<string, List<string>>();
    private static readonly IDictionary<string, UniTaskCompletionSource<List<string>>> PendingLocationLoads =
        new Dictionary<string, UniTaskCompletionSource<List<string>>>();

    public static void ClearResourceLoadCaches()
    {
        var series = new List<UniTaskCompletionSource<List<AnimationClip>>>(PendingSeriesLoads.Values);
        var locations = new List<UniTaskCompletionSource<List<string>>>(PendingLocationLoads.Values);
        PendingSeriesLoads.Clear();
        PendingLocationLoads.Clear();
        AnimationLocationKeys.Clear();
        foreach (var source in series) source.TrySetCanceled();
        foreach (var source in locations) source.TrySetCanceled();
    }

    int personalResourceCacheVersion = -1;
    private FacialAnimManager facialAnimManager;

    private static async UniTask<List<string>> LoadSharedAnimationLocationKeys(string label)
    {
        if (AnimationLocationKeys.TryGetValue(label, out var cachedKeys))
        {
            return cachedKeys;
        }

        if (PendingLocationLoads.TryGetValue(label, out var pendingLoad))
        {
            return await pendingLoad.Task;
        }

        var cacheVersion = AnimationResourceLoader.CacheVersion;
        var loadSource = new UniTaskCompletionSource<List<string>>();
        PendingLocationLoads.Add(label, loadSource);
        try
        {
            var loadedKeys = new List<string>();
            var locationHandle = Addressables.LoadResourceLocationsAsync(label);
            try
            {
                await locationHandle;
                if (locationHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException($"Failed to index animation label: {label}", locationHandle.OperationException);
                if (locationHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    foreach (var location in locationHandle.Result)
                    {
                        if (!string.IsNullOrEmpty(location.PrimaryKey))
                        {
                            loadedKeys.Add(location.PrimaryKey);
                        }
                    }
                }
            }
            finally
            {
                if (locationHandle.IsValid())
                    Addressables.Release(locationHandle);
            }

            if (cacheVersion != AnimationResourceLoader.CacheVersion)
                throw new OperationCanceledException("Animation label cache was cleared during loading.");
            AnimationLocationKeys.Add(label, loadedKeys);
            loadSource.TrySetResult(loadedKeys);
            return loadedKeys;
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException canceled)
                loadSource.TrySetCanceled(canceled.CancellationToken);
            else
                loadSource.TrySetException(exception);
            return await loadSource.Task;
        }
        finally
        {
            if (PendingLocationLoads.TryGetValue(label, out var current) && ReferenceEquals(current, loadSource))
                PendingLocationLoads.Remove(label);
        }
    }

    private static async UniTask<List<AnimationClip>> LoadSharedAnimationSeries(
        string seriesKey,
        Func<UniTask<List<AnimationClip>>> load)
    {
        if (AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(seriesKey, out var cachedClips))
        {
            return cachedClips;
        }

        if (PendingSeriesLoads.TryGetValue(seriesKey, out var pendingLoad))
        {
            return await pendingLoad.Task;
        }

        var cacheVersion = AnimationResourceLoader.CacheVersion;
        var loadSource = new UniTaskCompletionSource<List<AnimationClip>>();
        PendingSeriesLoads.Add(seriesKey, loadSource);
        try
        {
            var loadedClips = await load() ?? new List<AnimationClip>();
            if (cacheVersion != AnimationResourceLoader.CacheVersion)
                throw new OperationCanceledException("Animation series cache was cleared during loading.");
            if (AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(seriesKey, out cachedClips))
            {
                loadedClips = cachedClips;
            }
            else
            {
                AnimationResourceLoader.SeriesAnimationClipsDic.Add(seriesKey, loadedClips);
            }

            loadSource.TrySetResult(loadedClips);
            return loadedClips;
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException canceled)
                loadSource.TrySetCanceled(canceled.CancellationToken);
            else
                loadSource.TrySetException(exception);
            return await loadSource.Task;
        }
        finally
        {
            if (PendingSeriesLoads.TryGetValue(seriesKey, out var current) && ReferenceEquals(current, loadSource))
                PendingSeriesLoads.Remove(seriesKey);
        }
    }

    public void CasualFace()
    {
        if (facialAnimManager != null)
            facialAnimManager.CasualFace();
    }

    public void TriggerExpression(Facial facial)
    {
        facialAnimManager?.TriggerExpression(facial);
    }

    public async UniTask PreloadBasicPersonalAnims(
        string type,
        string basicPackName,
        FacialAnimManager facialAnimManager = null,
        Action<float> onProgress = null)
    {
        onProgress?.Invoke(0f);
        var basicPackKey = AnimationResourceKeyUtility.BasicPackSeriesKey(type, basicPackName);
        var basicAnims = await LoadSharedAnimationSeries(basicPackKey, async () =>
        {
            var loadedClips = new List<AnimationClip>();
            var locationKeys = await LoadSharedAnimationLocationKeys(AnimationResourceKeyUtility.BasicAnimationLabel);
            var checkedLocations = 0;
            foreach (var primaryKey in locationKeys)
            {
                if (AnimationResourceKeyUtility.IsBasicAnimationLocation(primaryKey, type, basicPackName))
                {
                    Object value = await AddressablesLogic.LoadT<AnimationClip>(primaryKey);
                    if (value != null)
                    {
                        loadedClips.Add((AnimationClip)value);
                    }
                }

                checkedLocations++;
                if (locationKeys.Count > 0)
                {
                    onProgress?.Invoke(Mathf.Lerp(0.05f, 0.3f, checkedLocations / (float)locationKeys.Count));
                }
            }
            return loadedClips;
        });
        onProgress?.Invoke(0.3f);

        toLoadAnims = new Dictionary<string, AnimationClip>();
        personalResourceCacheVersion = AnimationResourceLoader.CacheVersion;
        if (basicAnims != null)
        {
            foreach (var animationClip in basicAnims)
            {
                if (animationClip.name == "death")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("death", animationClip));
                }

                if (animationClip.name == "rush")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("rush", animationClip));
                }

                if (animationClip.name == "block")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("block", animationClip));
                }

                if (animationClip.name == "block_break")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("block_break", animationClip));
                }
                // if (animationClip.name == "dash")
                // {
                //     toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("dash", animationClip));
                // }
                // if (animationClip.name == "rushback")
                // {
                //     toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("rushback", animationClip));
                // }

                if (animationClip.name == "getup")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("getup", animationClip));
                }

                if (animationClip.name == "victory")
                {
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>("victory", animationClip));
                }
            }
        }
        else
        {
            Debug.Log("Basic Anim Pack Error:" + type + "  " + basicPackName);
        }

        async UniTask LoadHurtAnim(string type, string address, string label)
        {
            var key = AnimationResourceKeyUtility.SeriesKey(type, address);
            await LoadSharedAnimationSeries(key, async () =>
            {
                var loadedClips = new List<AnimationClip>();
                var locationKeys = await LoadSharedAnimationLocationKeys(label);
                foreach (var primaryKey in locationKeys)
                {
                    if (AnimationResourceKeyUtility.IsSeriesAnimationLocation(primaryKey, key))
                    {
                        Object value = await AddressablesLogic.LoadT<AnimationClip>(primaryKey);
                        if (value != null)
                        {
                            loadedClips.Add((AnimationClip)value);
                        }
                    }
                }
                return loadedClips;
            });
        }

        var hurtLoadFinished = 0;
        async UniTask TrackHurtAnim(UniTask task)
        {
            await task;
            hurtLoadFinished++;
            onProgress?.Invoke(Mathf.Lerp(0.3f, 0.75f, hurtLoadFinished / 6f));
        }

        await UniTask.WhenAll(
            TrackHurtAnim(LoadHurtAnim(type, "basic_hurts/back", "hurt_anim")),
            TrackHurtAnim(LoadHurtAnim(type, "basic_hurts/high", "hurt_anim")),
            TrackHurtAnim(LoadHurtAnim(type, "basic_hurts/lay", "hurt_anim")),
            TrackHurtAnim(LoadHurtAnim(type, "basic_hurts/low", "hurt_anim")),
            TrackHurtAnim(LoadHurtAnim(type, "basic_hurts/press", "hurt_anim")),
            TrackHurtAnim(LoadHurtAnim(type, "basic_knockoffs", "knock_anim"))
        );

        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_knockoffs"), out knockoffAnimations);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_hurts/back"), out _hurtClipsBack);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_hurts/low"), out _hurtClipsLow);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_hurts/high"), out _hurtClipsHigh);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_hurts/press"), out _hurtClipsPress);
        AnimationResourceLoader.SeriesAnimationClipsDic.TryGetValue(AnimationResourceKeyUtility.SeriesKey(type, "basic_hurts/lay"), out _hurtClipsLay);

        if (Animator == null || Animator.gameObject == null)
        {
            onProgress?.Invoke(1f);
            return; // When the character model is displayed, there may be issues such as the menu suddenly closing
        }
        if (Animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[AnimationCompat] Missing runtimeAnimatorController on {Animator.gameObject.name}");
            return;
        }
        animatorOverride = new AnimatorOverrideController(Animator.runtimeAnimatorController);
        if (animatorOverride.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[AnimationCompat] Missing override base controller on {Animator.gameObject.name}");
            return;
        }
        onProgress?.Invoke(0.85f);

        // 以上内容为个性化动画片段对base层基础动画的覆盖
        foreach (var animationClip in basicAnims)
        {
            if (animationClip.name == "idle")
            {
                if (animatorOverride["idle"])
                    animatorOverride["idle"] = animationClip;
            }

            if (animationClip.name == "walk")
            {
                if (animatorOverride["walk"])
                    animatorOverride["walk"] = animationClip;
            }

            if (animationClip.name == "run")
            {
                if (animatorOverride["run"])
                    animatorOverride["run"] = animationClip;
            }

            if (animationClip.name == "air")
            {
                if (animatorOverride["air"])
                    animatorOverride["air"] = animationClip;
            }
        }

        this.facialAnimManager = facialAnimManager;
        this.facialAnimManager?.INI(Animator, animatorOverride);
        Animator.runtimeAnimatorController = animatorOverride;
        onProgress?.Invoke(1f);
    }

    public async UniTask PreloadPersonalAnimResourceMode(
        string animPath,
        string key,
        Element element,
        int preloadCount,
        Action<float> onProgress = null)
    {
        var cacheVersion = AnimationResourceLoader.CacheVersion;
        if (personalResourceCacheVersion != cacheVersion)
        {
            toLoadAnims.Clear();
            personalResourceCacheVersion = cacheVersion;
        }
        onProgress?.Invoke(0f);
        if (toLoadAnims.ContainsKey(key))
        {
            onProgress?.Invoke(1f);
            return;
        }

        await AnimationResourceLoader.LoadAnim(animPath, key);
        onProgress?.Invoke(0.3f);
        var clip = AnimationResourceLoader.Instance.GetAnimationClip(AnimationResourceKeyUtility.SkillClipKey(animPath, key));
        if (clip != null)
        {
            if (!toLoadAnims.ContainsKey(key))
            {
                var tasks = new List<UniTask>();

                foreach (AnimationEvent e in clip.events)
                {
                    if (e.functionName == "MagicForward")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "MagicForwardOnBody")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "MagicToEnemy")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName == "PrepareOneMagic")
                    {
                        tasks.Add(HurtObjectManager.ConstructHurtObjectPool(e.stringParameter, element, preloadCount));
                    }
                    if (e.functionName is "Bullet_shoot_from_body_part" or "Bullet_shoot_from_body_part_TD")
                    {
                        switch (e.intParameter)
                        {
                            case 1:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("bullet", element, preloadCount));
                                break;
                            case 2:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("big_bullet", element, preloadCount));
                                break;
                            case 3:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("super_bullet", element, preloadCount));
                                break;
                            default:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("bullet", element, preloadCount));
                                break;
                        }
                    }
                    if (e.functionName == "BlastAttack")
                    {
                        switch (e.intParameter)
                        {
                            case 0:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, preloadCount));
                                break;
                            case 1:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, preloadCount));
                                break;
                            case 2:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("big_blast", element, preloadCount));
                                break;
                            default:
                                tasks.Add(HurtObjectManager.ConstructHurtObjectPool("blast", element, preloadCount));
                                break;
                        }
                    }
                    if (e.functionName == "PlaySoundOnce")
                    {
                        tasks.Add(AudioResourceLoading.Instance.LoadAudioClipFromResourceAndPutItIntoDic(
                            AudioResourceLoaderCore.EffectAudioPath, e.stringParameter));
                    }
                    // effects loading
                    if (e.functionName == "EffectOnBodyPart")
                    {
                        tasks.Add(EffectsManager.IniEffectsPool("normal_effect",
                            FightGlobalSetting.EffectPathDefine(element), preloadCount));
                    }

                    if (e.functionName == "Flash")
                    {
                        tasks.Add(EffectsManager.IniEffectsPool("FlashStart",
                            FightGlobalSetting.EffectPathDefine(element), preloadCount));
                        tasks.Add(EffectsManager.IniEffectsPool("FlashEnd",
                            FightGlobalSetting.EffectPathDefine(element), preloadCount));
                    }

                    if (e.functionName == "ResistanceUp")
                    {
                        if (e.stringParameter == "resistup")
                            tasks.Add(EffectsManager.IniEffectsPool(CommonSetting.BreakFreeEffectCode,
                                FightGlobalSetting.EffectPathDefine(), preloadCount));
                        if (e.stringParameter == "speedup")
                            tasks.Add(EffectsManager.IniEffectsPool("speedupbuff",
                                FightGlobalSetting.EffectPathDefine(element), preloadCount));
                    }
                }
                if (tasks.Count == 0)
                {
                    onProgress?.Invoke(1f);
                }
                else
                {
                    var completedResources = 0;
                    var trackedTasks = new List<UniTask>(tasks.Count);
                    async UniTask TrackResourceTask(UniTask task)
                    {
                        await task;
                        completedResources++;
                        onProgress?.Invoke(Mathf.Lerp(0.3f, 1f, completedResources / (float)tasks.Count));
                    }

                    foreach (var task in tasks)
                    {
                        trackedTasks.Add(TrackResourceTask(task));
                    }
                    await UniTask.WhenAll(trackedTasks);
                }
                if (cacheVersion != AnimationResourceLoader.CacheVersion)
                    throw new OperationCanceledException("Skill resources were cleared during loading.");
                if (!toLoadAnims.ContainsKey(key))
                    toLoadAnims.Add(new KeyValuePair<string, AnimationClip>(key, clip));
            }
        }
        onProgress?.Invoke(1f);
    }

    public async UniTask PreloadPersonalAnimsResourceMode(
        string type,
        List<string> toLoadSkillAnimsNames,
        Element element,
        int preloadCount,
        Action<float> onProgress = null)
    {
        const int maxConcurrentSkillPreloads = 3;
        onProgress?.Invoke(0f);
        if (toLoadSkillAnimsNames == null || toLoadSkillAnimsNames.Count == 0)
        {
            onProgress?.Invoke(1f);
            return;
        }

        var tasks = new List<UniTask>(maxConcurrentSkillPreloads);
        var completedSkills = 0;
        async UniTask TrackSkillLoad(string animName)
        {
            await PreloadPersonalAnimResourceMode(type, animName, element, preloadCount);
            completedSkills++;
            onProgress?.Invoke(completedSkills / (float)toLoadSkillAnimsNames.Count);
        }

        foreach (var animName in toLoadSkillAnimsNames)
        {
            tasks.Add(TrackSkillLoad(animName));
            if (tasks.Count < maxConcurrentSkillPreloads)
            {
                continue;
            }

            await UniTask.WhenAll(tasks);
            tasks.Clear();
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
        }
        onProgress?.Invoke(1f);
    }
}

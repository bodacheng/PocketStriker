using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>An optional background story can never fail its battle caller.</summary>
public static class BattleStoryRequest
{
    public static async UniTask<StoryInfo> Load(Func<UniTask<StoryInfo>> request,
        CancellationToken cancellationToken, double timeoutSeconds = 60)
    {
        try
        {
            var story = await request().AttachExternalCancellation(cancellationToken)
                .Timeout(TimeSpan.FromSeconds(timeoutSeconds), DelayType.Realtime);
            // Empty/malformed responses use the authored story or normal result.
            return story != null && story.HasVisualScene() ? story : null;
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception exception)
        {
            Debug.LogWarning("[FightScene] Optional AI story unavailable: " + exception.Message);
            return null;
        }
    }
}

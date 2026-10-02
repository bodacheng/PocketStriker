using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Real production transport acceptance; reports exclude prompts, identities and signed URLs.</summary>
public static class PocketStrikerQueuedLiveValidation
{
    [Serializable] public sealed class Call
    {
        public string operation, kind, id, status, error;
        public double seconds;
        public int generationAttempts;
    }
    [Serializable] public sealed class Report
    {
        public bool passed, coldText, coldImage, duplicateSharedJob, consumerCancelled, recoveredAfterCancel;
        public bool warmSameImage, invalidInputRejected, missingJobHandled;
        public double coldSeconds, warmSeconds, maximumHttpSeconds;
        public int uniqueJobs, generationAttempts;
        public string imageHash, dimensions, phase, failure, reusedColdTextEvidence;
        public List<Call> calls = new List<Call>();
        public string limits = "Real PlayFab/Azure account requests. Cold means uncached content, not a guaranteed cold Azure host. Provider outage and worker-crash retry are tested with local fault fixtures, not induced on the shared live service. No provider billing amount is available.";
    }
    public static void EnableClientBatch()
    {
        var service = JsonUtility.FromJson<Report>(File.ReadAllText("Logs/AIStory/Live/queued-service/queued-service.json"));
        var live = JsonUtility.FromJson<PocketStrikerAIStoryLiveSmoke.Report>(File.ReadAllText("Logs/AIStory/Live/queued-service/report.json"));
        Require(service.passed && live.passed && live.legacyCompatible, "Live service acceptance is required before enabling the client.");
        var target = UnityEditor.Build.NamedBuildTarget.iOS;
        var symbols = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(value => !string.IsNullOrEmpty(value)).ToList();
        if (!symbols.Contains("POCKETSTRIKER_QUEUED_STORIES")) symbols.Add("POCKETSTRIKER_QUEUED_STORIES");
        UnityEditor.PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        UnityEditor.AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/AIStory/Live/queued-service/client-enabled.json", "{\"enabled\":true,\"target\":\"iOS\",\"serviceAccepted\":true}");
    }

    static Report report;
    static string output;
    static UniTask duplicateTask;
    static bool launchedDuplicate;
    static string priorColdTextId;
    static readonly List<PocketStrikerStoryJobClient.Request> starts = new List<PocketStrikerStoryJobClient.Request>();

    public static async UniTask<StoryInfo> Validate(CancellationToken ct, string directory)
    {
        output = Path.Combine(directory, "queued-service.json");
        priorColdTextId = null;
        if (File.Exists(output))
        {
            var previous = JsonUtility.FromJson<Report>(File.ReadAllText(output));
            priorColdTextId = previous.calls.FirstOrDefault(call => call.kind == "text" && (call.status == "queued" || call.status == "running"))?.id;
        }
        report = new Report { phase = "cold" };
        starts.Clear(); launchedDuplicate = false;
        PocketStrikerStoryJobClient.ValidationObserver = Observe;
        StoryInfo cold = null, warm = null;
        try
        {
            double began = Time.realtimeSinceStartupAsDouble;
            cold = await PocketStrikerQueuedStory.Load(ct);
            report.coldSeconds = Time.realtimeSinceStartupAsDouble - began;
            Require(launchedDuplicate, "Cold image job did not expose a pending state.");
            await duplicateTask;
            report.recoveredAfterCancel = report.consumerCancelled && cold.HasVisualScene();
            report.imageHash = ImageHash(cold);
            var texture = cold.StoryScenes[0].Pic.texture;
            report.dimensions = texture.width + "x" + texture.height;
            report.phase = "warm"; Save();
            int beforeWarm = report.calls.Count;
            began = Time.realtimeSinceStartupAsDouble;
            warm = await PocketStrikerQueuedStory.Load(ct);
            report.warmSeconds = Time.realtimeSinceStartupAsDouble - began;
            report.warmSameImage = ImageHash(warm) == report.imageHash
                && report.calls.Skip(beforeWarm).Count() == 2
                && report.calls.Skip(beforeWarm).All(call => call.operation == "start" && call.status == "ready" && call.generationAttempts == 1);
            report.phase = "validation-errors"; Save();
            try
            {
                await PocketStrikerStoryJobClient.SendForValidation(new PocketStrikerStoryJobClient.Request
                    { operation = "start", kind = "text", input = new PocketStrikerStoryJobClient.Input { prompt = "" } }, ct);
            }
            catch (InvalidOperationException) { report.invalidInputRejected = true; }
            var missing = await PocketStrikerStoryJobClient.SendForValidation(new PocketStrikerStoryJobClient.Request
                { operation = "status", id = new string('0', 64) }, ct);
            report.missingJobHandled = missing.protocol == PocketStrikerStoryJobClient.Protocol && missing.status == "missing";
            var completed = report.calls.Where(call => call.status == "ready").GroupBy(call => call.id).ToArray();
            report.uniqueJobs = completed.Length;
            report.generationAttempts = completed.Sum(group => group.Max(call => call.generationAttempts));
            report.maximumHttpSeconds = report.calls.Max(call => call.seconds);
            report.passed = report.coldText && report.coldImage && report.duplicateSharedJob && report.consumerCancelled
                && report.recoveredAfterCancel && report.warmSameImage && report.invalidInputRejected && report.missingJobHandled
                && report.uniqueJobs == 2 && report.generationAttempts == 2 && report.maximumHttpSeconds < 10;
            report.phase = "complete"; Save();
            Require(report.passed, "Queued service acceptance failed; inspect sanitized service report.");
            var result = cold; cold = null;
            return result;
        }
        catch (Exception error)
        {
            report.failure = error.GetType().Name + ": " + (error is InvalidOperationException ? error.Message : "See test phase.");
            throw;
        }
        finally
        {
            PocketStrikerStoryJobClient.ValidationObserver = null;
            PocketStrikerQueuedStory.Release(cold);
            PocketStrikerQueuedStory.Release(warm);
            Save();
        }
    }

    static void Observe(PocketStrikerStoryJobClient.Request request, PocketStrikerStoryJobClient.Reply reply, double seconds, string error)
    {
        report.calls.Add(new Call { operation = request.operation, kind = request.kind, id = reply?.id,
            status = reply?.status, error = error ?? reply?.error, seconds = seconds, generationAttempts = reply?.generationAttempts ?? 0 });
        if (!string.IsNullOrEmpty(reply?.result?.text))
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "generated-text.txt"), reply.result.text);
        if (request.operation == "start" && reply != null && !starts.Any(value => value.kind == request.kind))
        {
            starts.Add(request);
            bool cold = reply.status == "queued" || reply.status == "running";
            if (request.kind == "text")
            {
                report.coldText = cold || reply.id == priorColdTextId;
                if (!cold && reply.id == priorColdTextId) report.reusedColdTextEvidence = "Same job was queued then ready in preserved ServiceFirstText/queued-service.json; replay reuses that paid result.";
            }
            if (request.kind == "image")
            {
                report.coldImage = cold;
                if (cold && !launchedDuplicate)
                {
                    launchedDuplicate = true;
                    duplicateTask = DuplicateAndCancel(request, reply.id).Preserve();
                }
            }
        }
        Save();
    }

    static async UniTask DuplicateAndCancel(PocketStrikerStoryJobClient.Request original, string expectedId)
    {
        var duplicate = await PocketStrikerStoryJobClient.SendForValidation(original, default);
        report.duplicateSharedJob = duplicate.id == expectedId && duplicate.generationAttempts <= 1;
        using var cancel = new CancellationTokenSource();
        var client = new PocketStrikerStoryJobClient(async (request, token) =>
        {
            var reply = await PocketStrikerStoryJobClient.SendForValidation(request, token);
            Require(reply.id == expectedId, "Duplicate started another job.");
            cancel.Cancel();
            return reply;
        });
        try { await client.Generate(original.kind, original.input.prompt, cancel.Token); }
        catch (OperationCanceledException) { report.consumerCancelled = true; }
        Save();
    }

    public static string ImageHash(StoryInfo story)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(story.StoryScenes[0].Pic.texture.EncodeToPNG())).Replace("-", "").ToLowerInvariant();
    }
    static void Save() { if (report != null) File.WriteAllText(output, JsonUtility.ToJson(report, true)); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

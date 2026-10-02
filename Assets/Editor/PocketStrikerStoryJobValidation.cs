using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class PocketStrikerStoryJobValidation
{
    [Serializable] sealed class Report { public bool passed; public List<string> checks = new List<string>(); public List<string> failures = new List<string>(); }
    public static void ValidateBatch() => Run().Forget();
    static async UniTask Run()
    {
        var report = new Report();
        async UniTask Check(string name, Func<UniTask> run) { try { await run(); report.checks.Add(name); } catch(Exception e) {report.failures.Add(name + ": " + e.Message);} }
        PocketStrikerStoryJobClient.Reply Reply(string status, string id = "job") => new PocketStrikerStoryJobClient.Reply { protocol = PocketStrikerStoryJobClient.Protocol, id = id, status = status,
            result = status == "ready" ? new PocketStrikerStoryJobClient.Result { text = "story", images = new[]{new PocketStrikerStoryJobClient.Image {url="https://example.invalid/image"}} } : null, retryAfterMs = 2000 };
        UniTask Delay(int _, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return UniTask.CompletedTask; }
        await Check("cold-pending-polls-status-only", async () => {
            var calls = new List<string>();
            var client = new PocketStrikerStoryJobClient((r,ct) => {calls.Add(r.operation); return UniTask.FromResult(Reply(calls.Count == 1 ? "queued" : calls.Count == 2 ? "running" : "ready"));}, Delay);
            var result = await client.Generate("text", "new input", default);
            Require(result.text == "story" && string.Join(",", calls) == "start,status,status", "Unexpected resend or incomplete result.");
        });
        await Check("warm-result-never-polls", async () => {
            int calls=0;var client=new PocketStrikerStoryJobClient((r,ct)=>{calls++;return UniTask.FromResult(Reply("ready"));},Delay);
            await client.Generate("image","warm input",default);Require(calls==1,"Warm request sent more than once.");
        });
        foreach(var status in new[]{"failed","missing","unavailable"}) await Check(status+"-does-not-regenerate",async()=>{
            int calls=0;var client=new PocketStrikerStoryJobClient((r,ct)=>{calls++;return UniTask.FromResult(Reply(status));},Delay);
            await Reject(()=>client.Generate("text","input",default));Require(calls==1,"Failed request regenerated automatically.");
        });
        await Check("old-server-response-rejected",async()=>{var client=new PocketStrikerStoryJobClient((r,ct)=>UniTask.FromResult(new PocketStrikerStoryJobClient.Reply{status="ready"}),Delay);await Reject(()=>client.Generate("text","input",default));});
        await Check("changed-job-identity-rejected",async()=>{int calls=0;var client=new PocketStrikerStoryJobClient((r,ct)=>UniTask.FromResult(++calls==1?Reply("queued","first"):Reply("ready","other")),Delay);await Reject(()=>client.Generate("text","input",default));});
        await Check("empty-ready-result-rejected",async()=>{var ready=Reply("ready");ready.result=null;var client=new PocketStrikerStoryJobClient((r,ct)=>UniTask.FromResult(ready),Delay);await Reject(()=>client.Generate("image","input",default));});
        await Check("cancel-in-flight-no-late-poll",async()=>{
            using var cancel=new CancellationTokenSource();var pending=new UniTaskCompletionSource<PocketStrikerStoryJobClient.Reply>();int calls=0;
            var client=new PocketStrikerStoryJobClient((r,ct)=>{calls++;return pending.Task;},Delay);
            var task=client.Generate("text","input",cancel.Token);cancel.Cancel();
            bool canceled=false;try{await task;}catch(OperationCanceledException){canceled=true;}pending.TrySetResult(Reply("queued"));Require(canceled&&calls==1,"Cancellation continued polling.");
        });
        await Check("timeout-in-flight-bounded",async()=>{
            var pending=new UniTaskCompletionSource<PocketStrikerStoryJobClient.Reply>();int calls=0;
            var client=new PocketStrikerStoryJobClient((r,ct)=>{calls++;return pending.Task;},Delay);bool timed=false;
            try{await client.Generate("text","input",default,30);}catch(TimeoutException){timed=true;}pending.TrySetResult(Reply("queued"));Require(timed&&calls==1,"Deadline did not stop pending transport.");
        });
        await Check("explicit-new-attempt-retries-previous-failure-only-once",async()=>{
            var calls=new List<string>();var client=new PocketStrikerStoryJobClient((r,ct)=>{calls.Add(r.operation);return UniTask.FromResult(Reply(calls.Count==1?"failed":calls.Count==2?"queued":"ready"));},Delay);
            await client.Generate("text","input",default,retryFailed:true);Require(string.Join(",",calls)=="start,retry,status","Explicit retry sequence changed.");
        });
        await Check("transient-start-and-status-recover-same-job-within-one-budget", async () => {
            var calls = new List<PocketStrikerStoryJobClient.Request>(); int delays = 0;
            var client = new PocketStrikerStoryJobClient((r, ct) => {
                calls.Add(r);
                if (calls.Count == 1 || calls.Count == 3)
                    return UniTask.FromException<PocketStrikerStoryJobClient.Reply>(new PocketStrikerStoryJobClient.TransientRequestException());
                return UniTask.FromResult(Reply(calls.Count == 2 ? "queued" : "ready", "same-job"));
            }, (ms, ct) => { ct.ThrowIfCancellationRequested(); delays++; return UniTask.CompletedTask; });
            var result = await client.Generate("text", "same input", default);
            Require(result.text == "story" && calls.Count == 4 && delays == 3, "Recovery was unbounded or missing.");
            Require(calls[0].operation == "start" && ReferenceEquals(calls[0], calls[1])
                && calls[2].operation == "status" && ReferenceEquals(calls[2], calls[3]) && calls[2].id == "same-job", "Recovery created a new request or generation.");
        });
        await Check("transient-recovery-exhausts-at-two-retries", async () => {
            int calls = 0;
            var client = new PocketStrikerStoryJobClient((r, ct) => { calls++; return UniTask.FromException<PocketStrikerStoryJobClient.Reply>(new PocketStrikerStoryJobClient.TransientRequestException()); }, Delay);
            await Reject(() => client.Generate("image", "input", default)); Require(calls == 3, "Recovery exceeded its budget.");
        });
        await Check("permanent-transport-failure-is-not-retried", async () => {
            int calls = 0;
            var client = new PocketStrikerStoryJobClient((r, ct) => { calls++; return UniTask.FromException<PocketStrikerStoryJobClient.Reply>(new InvalidOperationException("permanent fixture error")); }, Delay);
            await Reject(() => client.Generate("text", "input", default)); Require(calls == 1, "Permanent failure retried.");
        });
        await Check("cancel-during-recovery-prevents-resend", async () => {
            using var cancel = new CancellationTokenSource(); int calls = 0;
            var client = new PocketStrikerStoryJobClient((r, ct) => { calls++; return UniTask.FromException<PocketStrikerStoryJobClient.Reply>(new PocketStrikerStoryJobClient.TransientRequestException()); },
                (ms, ct) => { cancel.Cancel(); return UniTask.FromCanceled(ct); });
            bool cancelled = false; try { await client.Generate("text", "input", cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
            Require(cancelled && calls == 1, "Cancelled recovery resent a request.");
        });
        await Check("recovery-respects-server-retry-after", async () => {
            int calls = 0, wait = 0;
            var client = new PocketStrikerStoryJobClient((r, ct) => ++calls == 1
                ? UniTask.FromException<PocketStrikerStoryJobClient.Reply>(new PocketStrikerStoryJobClient.TransientRequestException(7000)) : UniTask.FromResult(Reply("ready")),
                (ms, ct) => { wait = ms; return UniTask.CompletedTask; });
            await client.Generate("text", "input", default); Require(wait == 7000 && calls == 2, "Retry-After was ignored.");
        });
        await Check("transient-classification-excludes-permanent-http-errors", () => {
            foreach (var code in new[] {408, 429, 500, 502, 503})
                Require(PocketStrikerStoryJobClient.IsTransientTransport(new PlayFab.PlayFabError {HttpCode = code}), "Transient HTTP failure misclassified.");
            foreach (var code in new[] {400, 401, 403, 404})
                Require(!PocketStrikerStoryJobClient.IsTransientTransport(new PlayFab.PlayFabError {HttpCode = code, Error = PlayFab.PlayFabErrorCode.InvalidParams}), "Permanent HTTP failure would retry.");
            Require(!PocketStrikerStoryJobClient.IsTransientTransport(new PlayFab.PlayFabError {HttpCode = 401, Error = PlayFab.PlayFabErrorCode.ServiceUnavailable}), "Permanent HTTP result lost precedence.");
            Require(PocketStrikerStoryJobClient.IsTransientTransport(new PlayFab.PlayFabError {HttpCode = 0, Error = PlayFab.PlayFabErrorCode.ConnectionError}), "Connection failure was missed.");
            return UniTask.CompletedTask;
        });
        await Check("unity-http-sdk-fallback-retains-transient-versus-auth-errors", () => {
            PlayFab.PlayFabError Wrapped(string message) => new PlayFab.PlayFabError {
                HttpCode = 400, HttpStatus = "BadRequest", Error = PlayFab.PlayFabErrorCode.ServiceUnavailable, ErrorMessage = message };
            foreach (var message in new[] {"HTTP/1.1 503 Service Unavailable", "HTTP/2 429 Too Many Requests", "Cannot resolve destination host", "Cannot connect to destination host", "Request timeout"})
                Require(PocketStrikerStoryJobClient.IsTransientTransport(Wrapped(message)), "SDK-wrapped transient failure was missed.");
            foreach (var message in new[] {"HTTP/1.1 401 Unauthorized", "HTTP/1.1 403 Forbidden", "HTTP/2 400 Bad Request", "Malformed request", "SSL certificate error"})
                Require(!PocketStrikerStoryJobClient.IsTransientTransport(Wrapped(message)), "SDK-wrapped permanent failure would retry.");
            return UniTask.CompletedTask;
        });
        await Check("terminal-worker-failure-after-poll-never-retries-generation", async () => {
            var calls = new List<string>(); var client = new PocketStrikerStoryJobClient((r, ct) => {calls.Add(r.operation); return UniTask.FromResult(Reply(calls.Count == 1 ? "queued" : "failed"));}, Delay);
            await Reject(() => client.Generate("text", "input", default, retryFailed: true));
            Require(string.Join(",", calls) == "start,status", "A polled worker failure regenerated.");
        });
        await Check("gemini-json-fences-normalized", () => {
            const string json = "{\"title\":\"共闘\"}";
            foreach (string value in new[] {json, "  " + json + "  ", "```json\n" + json + "\n```", "```\n" + json + "\n```"})
                Require(PocketStrikerQueuedStory.NormalizeJson(value) == json, "Valid generated JSON changed.");
            return UniTask.CompletedTask;
        });
        await Check("incomplete-or-prose-json-rejected", () => {
            foreach (string value in new[] {"```json\n{}", "A story: {}", "{} trailing", "```xml\n{}\n```", "", "[]"})
            {
                bool rejected = false; try { PocketStrikerQueuedStory.NormalizeJson(value); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Malformed generated JSON was accepted.");
            }
            return UniTask.CompletedTask;
        });
        report.passed=report.failures.Count==0;Directory.CreateDirectory("Logs/AIStory/Queued");File.WriteAllText("Logs/AIStory/Queued/report.json",JsonUtility.ToJson(report,true));
        Debug.Log("[StoryJobValidation] "+(report.passed?"PASS":"FAIL")+": "+report.checks.Count+" checks");EditorApplication.Exit(report.passed?0:1);
    }
    static async UniTask Reject(Func<UniTask<PocketStrikerStoryJobClient.Result>> action){bool threw=false;try{await action();}catch(InvalidOperationException){threw=true;}Require(threw,"Invalid response was accepted.");}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}

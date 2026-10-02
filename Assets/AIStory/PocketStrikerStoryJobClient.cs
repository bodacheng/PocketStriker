using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.Json;
using UnityEngine;

/// <summary>Opt-in project adapter for the queued server protocol. Never resends generation while polling.</summary>
public sealed class PocketStrikerStoryJobClient
{
    public const string Protocol = "pocket-story-jobs-v1";
    public const string FunctionName = "generateGeminiImages";
    public sealed class TransientRequestException : InvalidOperationException
    {
        public int RetryAfterMs { get; }
        public TransientRequestException(int retryAfterMs = 0) : base("Story request is temporarily unavailable.")
        { RetryAfterMs = retryAfterMs; }
    }
    // SDK transport failures and a host cold-start timeout are safe to retry:
    // start keys are deterministic and status never generates. Permanent failures remain terminal.
    public static bool IsTransientTransport(PlayFabError error)
    {
        if (error == null) return false;
        if (TemporaryHttpCode(error.HttpCode)) return true;
        // PlayFabUnityHttp.OnError passes plain UnityWebRequest.error to GeneratePlayFabError.
        // This SDK maps unparseable strings to 400/BadRequest/ServiceUnavailable, losing the true status.
        if (error.HttpCode == 400 && error.Error == PlayFabErrorCode.ServiceUnavailable && error.HttpStatus == "BadRequest")
        {
            string message = error.ErrorMessage ?? "";
            if (message.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = message.Split(' ');
                return parts.Length > 1 && int.TryParse(parts[1], out var status) && TemporaryHttpCode(status);
            }
            if (message.StartsWith("Cannot resolve destination host", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Cannot connect to destination host", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Failed to receive data", StringComparison.OrdinalIgnoreCase)
                || message.Equals("Request timeout", StringComparison.OrdinalIgnoreCase)
                || message.Equals("Timeout was reached", StringComparison.OrdinalIgnoreCase)) return true;
        }
        // An explicit permanent HTTP result takes precedence over an SDK fallback category.
        if (error.HttpCode >= 400 && error.HttpCode < 500) return false;
        return error.Error == PlayFabErrorCode.ConnectionError || error.Error == PlayFabErrorCode.ServiceUnavailable
            || error.Error == PlayFabErrorCode.DownstreamServiceUnavailable || error.Error == PlayFabErrorCode.InternalServerError
            || error.Error == PlayFabErrorCode.APIRequestLimitExceeded;
    }

    static bool TemporaryHttpCode(int status) => status == 408 || status == 429 || (status >= 500 && status <= 599);
    [Serializable] public sealed class Image { public string url; public string mimeType; }
    [Serializable] public sealed class Result { public string text; public Image[] images; }
    [Serializable] public sealed class Reply
    {
        public string protocol, id, status, error;
        public int retryAfterMs, generationAttempts;
        public Result result;
    }
    [Serializable] public sealed class Input { public string prompt; }
    [Serializable] public sealed class Request { public string operation, kind, id; public Input input; }
    readonly Func<Request, CancellationToken, UniTask<Reply>> execute;
    readonly Func<int, CancellationToken, UniTask> delay;

    public PocketStrikerStoryJobClient(Func<Request, CancellationToken, UniTask<Reply>> execute = null,
        Func<int, CancellationToken, UniTask> delay = null)
    {
        this.execute = execute ?? Execute;
        this.delay = delay ?? ((ms, ct) => UniTask.Delay(ms, DelayType.Realtime, cancellationToken: ct));
    }

    public async UniTask<Result> Generate(string kind, string prompt, CancellationToken cancellationToken, int timeoutMs = 90000, bool retryFailed = false)
    {
        if (kind != "text" && kind != "image") throw new ArgumentException("Invalid story job kind.");
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Missing story prompt.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        int recoveries = 0;
        async UniTask<Reply> Send(Request request)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try { return await execute(request, token).AttachExternalCancellation(token); }
                catch (TransientRequestException error) when (recoveries < 2)
                {
                    recoveries++;
                    // The existing deadline bounds retries and Retry-After. Cancellation stops before resending.
                    await delay(Math.Max(1000 * recoveries, error.RetryAfterMs), token).AttachExternalCancellation(token);
                }
            }
        }
        try
        {
            token.ThrowIfCancellationRequested();
            var response = await Send(new Request { operation = "start", kind = kind, input = new Input { prompt = prompt } });
            // Only a new, explicitly requested battle attempt may retry a previously failed job.
            // Failures observed after polling begins are returned without automatic regeneration.
            if (retryFailed && response?.protocol == Protocol && response.status == "failed" && !string.IsNullOrEmpty(response.id))
                response = await execute(new Request { operation = "retry", id = response.id }, token).AttachExternalCancellation(token);
            string jobId = null;
            for (int poll = 0; poll < 60; poll++)
            {
                token.ThrowIfCancellationRequested();
                if (response == null || response.protocol != Protocol || string.IsNullOrEmpty(response.id))
                    throw new InvalidOperationException("Story server does not support the queued protocol.");
                if (jobId != null && jobId != response.id) throw new InvalidOperationException("Story job identity changed.");
                jobId = response.id;
                if (response.status == "ready")
                {
                    bool valid = kind == "text" ? !string.IsNullOrWhiteSpace(response.result?.text)
                        : response.result?.images?.Length > 0 && !string.IsNullOrEmpty(response.result.images[0]?.url);
                    if (!valid) throw new InvalidOperationException("Completed story job has no usable result.");
                    return response.result;
                }
                if (response.status != "queued" && response.status != "running")
                    throw new InvalidOperationException("Story job is unavailable (" + response.status + ").");
                await delay(Mathf.Clamp(response.retryAfterMs, 1000, 5000), token).AttachExternalCancellation(token);
                response = await Send(new Request { operation = "status", id = jobId });
            }
            throw new TimeoutException("Story job polling limit exceeded.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("Story job deadline exceeded."); }
        finally { await UniTask.SwitchToMainThread(); }
    }

#if UNITY_EDITOR
    public static Action<Request, Reply, double, string> ValidationObserver;
    public static UniTask<Reply> SendForValidation(Request request, CancellationToken token) => Execute(request, token);
#endif
    static UniTask<Reply> Execute(Request request, CancellationToken cancellationToken)
    {
        var source = new UniTaskCompletionSource<Reply>();
#if UNITY_EDITOR
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        void Observe(Reply reply, string error = null) => ValidationObserver?.Invoke(request, reply, stopwatch.Elapsed.TotalSeconds, error);
#endif
        cancellationToken.ThrowIfCancellationRequested();
        PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest
        {
            FunctionName = FunctionName,
            FunctionParameter = new { storyJob = request },
            GeneratePlayStreamEvent = false
        }, response =>
        {
            if (cancellationToken.IsCancellationRequested) { source.TrySetCanceled(cancellationToken); return; }
            if (response.Error != null)
            {
#if UNITY_EDITOR
                Observe(null, "FUNCTION_FAILED:" + response.Error.Error);
#endif
                bool transient = response.Error.Error == "CloudScriptAzureFunctionsExecutionTimeLimitExceeded"
                    || response.Error.Error == "CloudScriptAzureFunctionsHTTPRequestError";
                source.TrySetException(transient ? new TransientRequestException() : new InvalidOperationException("Story function failed.")); return;
            }
            try
            {
                var reply = JsonUtility.FromJson<Reply>(PlayFabSimpleJson.SerializeObject(response.FunctionResult));
#if UNITY_EDITOR
                Observe(reply);
#endif
                source.TrySetResult(reply);
            }
            catch { source.TrySetException(new InvalidOperationException("Invalid story job response.")); }
        }, error => {
            if (cancellationToken.IsCancellationRequested) { source.TrySetCanceled(cancellationToken); return; }
#if UNITY_EDITOR
            Observe(null, "TRANSPORT_FAILED:" + error.HttpCode + ":" + error.Error);
#endif
            int retryAfterMs = (int)Math.Min(int.MaxValue, (long)(error.RetryAfterSeconds ?? 0) * 1000);
            source.TrySetException(IsTransientTransport(error) ? new TransientRequestException(retryAfterMs)
                : new InvalidOperationException("Story transport failed."));
        });
        // Cancellation stops the local consumer and polling, not a shared server job.
        return source.Task.AttachExternalCancellation(cancellationToken);
    }
}

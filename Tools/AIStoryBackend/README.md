# PocketStriker AI story queue — deployed and accepted

As of 2026-10-01. The user approved the shared Azure update, limited real generation tests and local client enablement. Version `pocketstriker-story-jobs-20261001T142602Z-f65200fc` is active; deployment ID `4390e9a5f371482d91e4aa209d1d3272`. The iOS client now defines `POCKETSTRIKER_QUEUED_STORIES`. No new credentials/permissions, Git push, Addressables publication or app-store publication occurred.

## Accepted live behavior

- Full original active package `20260826072143.zip` was saved before deployment: 7,679,501 bytes, 4,545 ZIP entries, SHA256 `4e541789e31001e7aca69ab62efde0dc60b6c8ded3d1122a5cc03666b358bf84`. A second complete runtime archive and deployment history were preserved. Original deployment: `710f175736cc45efa8c896bd65284fd4`.
- The worker completed two unique real jobs: one text and one image, each with `generationAttempts: 1`. The first cold text job was queued then ready; its returned Markdown-fenced JSON exposed a client parsing defect, now repaired without repeating the paid text job.
- The replay used that cached text and a genuinely cold image: image pipeline/download 13.982 s; same-input warm pipeline 1.433 s. All measured PlayFab HTTP callbacks across both runs were at most 0.748 s. This is uncached-content acceptance, not a measurement of Azure host cold start. A single uninterrupted all-cold text+image pipeline was not timed.
- Concurrent identical starts shared the same ID; canceling one consumer did not cancel shared work or produce another generation. Invalid input was rejected, missing jobs remained missing, and the 1376×768 actual image downloaded successfully. Old-style text/image requests succeeded in 2.865 s; MCombat's separate full client was not run.
- After service acceptance, two real-account natural completed-Quest-4 fixture battles passed generated-image stability, captions, native continue/result/home navigation and repeated entry. They deliberately warmed the cache before combat. Optional-story exceptions, permanent waits and empty responses separately passed three natural fixture battles without blocking results.
- Final 13 client protocol/parser checks, 8 lifecycle checks, iOS script compilation and project check passed (3 scenes, 494 Addressables). The earlier 7 server unit cases and 5 local Azurite groups remain green; provider failures/lease recovery are local fault tests, not production outages.
- Evidence: `/Users/daisei/Documents/Codex/2026-09-30/task-2/LoginStoryReview/2026-10-01/Deployment/`. Never distribute raw editor/provider logs or signed image URLs.

The remaining sections retain the reviewed design, constraints and rollback procedure.

## Confirmed failure and evidence

The registered PlayFab HTTP call to `generateGeminiImages` returns:

> The function generateGeminiImages was terminated after the maximum execution time limit: 10000ms

The real development-account single-page cold run completed after **15.667 s**, with one text response and no image response. A subsequent same-cache run completed in **2.715 s** with a real 1376×768 image and actual battle → story → result → home navigation. Reports are preserved under `AIStoryReview/2026-10-01/AfterCold` and `AfterWarmFinal`. Raw logs contain signed image URLs and must not be distributed.

Read-only Azure source inspection now confirms that the HTTP handler awaits Gemini image generation and Blob upload inline. It checks completed cache files but has no job queue or in-flight lock. The provider timeout argument is 60 seconds, exceeding the PlayFab call's 10-second limit. This establishes the synchronous boundary failure; it does **not** separately measure Azure cold-start time or prove a Gemini model cold-start defect. A second source defect cancels AbortController timing after headers, before reading the body; the candidate fixes that for both text and image functions.

## Exact targets

- Subscription: `4a83df80-3bb9-4d73-ab4a-cc04b5d8065b`.
- Resource group: `rg-app-251117213249`.
- Function App: `mcombatCC`; current host `mcombatcc-aserdrf0arcmbdbc.canadacentral-01.azurewebsites.net`.
- Existing plan: `ASP-rgapp251117213249-9835`, **Y1 / Dynamic (Consumption)**.
- Existing non-secret storage-account setting: `app25111721324902461`, **StorageV2 / Standard_LRS**. The implementation reuses the same existing `AZURE_STORAGE_CONNECTION_STRING` as the current functions; it does not introduce or export an account key.
- Update: `generateGeminiImages/index.js`, `generateGeminiText/index.js`, dependency manifests and `host.json`.
- Add: internal `storyJobs` modules and queue-trigger function `processStoryJob`.
- New objects in the existing configured storage: Blob container **`pocketstriker-story-jobs`** and Queue **`pocketstriker-story-jobs`**. Azure may create **`pocketstriker-story-jobs-poison`** for exhausted deliveries.
- No new subscription, Function App, hosting plan, storage account, PlayFab function registration, DNS, app setting, credential or access-role grant is planned.

## Behavior and compatibility

New clients send a `storyJob` envelope to the already registered `generateGeminiImages` function. Start returns a deterministic job ID and queued/ready state. Status calls never initiate generation. The worker independently runs the existing text/image implementations. Blob leases prevent concurrent paid generation for the same job, including duplicate queue deliveries. Ready image jobs store Blob names, not signed URLs; status supplies fresh read URLs. HTTP storage operations have a seven-second outer response deadline. Interrupted initial enqueue can be recovered by repeating start without generating twice.

A failed job is terminal during ordinary start/status. An explicit new battle attempt may request retry after a 30-second cooldown. Each deterministic job is limited to **three generation attempts total**, including the initial attempt. Abandoned running jobs can be retried after 120 seconds when their lease is available. Client cancellation stops consumption and polling; it does not cancel a job shared by another request. Results remain optional and never block battle results.

PocketStriker and MCombat currently share PlayFab Title **38054** and this Function App. Requests without `storyJob` retain the existing response shapes, model handling and caches. Blob SDK remains at the deployed **12.33.0**, Queue SDK is pinned at **12.32.0**; the lock file pins the candidate dependency graph. The MCombat shared Unity package and its gitlink remain untouched. PocketStriker's project adapter is gated by `POCKETSTRIKER_QUEUED_STORIES`, now enabled for iOS after live acceptance. Other build targets keep their existing define sets. The October 3 client update uses 18 diverse story subjects with independently shuffled twists and tones, a stable seed per actual battle attempt, and explicit 2D cartoon art direction. Both the queue and legacy transport consume the same selected subject and art prompts; these client features work with the existing deployed service. Standard Gemini JSON fences are accepted, malformed/incomplete content falls back safely, and image/texture ownership is released with the battle scene. October 1 live acceptance above covers the previous prompts; the updated variety and art direction require a newly built client for live visual verification.

The October 3 local server source also isolates new queued image indexes using `storyId = input.cacheKey`, replacing the growing global `pocketstriker.json` index. This assignment occurs after job-ID hashing, so protocol, deterministic IDs, old ready jobs and their existing image locations are preserved. Nine local server unit cases pass. This server hardening has **not been deployed**; publish it through the normal backed-up deployment procedure to enable index isolation on the queue worker. The new legacy client sends its own cache key as `storyId` immediately, without a server change. No existing blobs or job records are deleted or migrated.

Deploying this package restarts the shared Function App. Both games may see a brief failure window. Old MCombat cold-image requests retain their synchronous limitation until that client adopts the new protocol; compatibility is not a claim that MCombat's cold path is fixed.

## Costs and permissions

Uses the existing Consumption plan and storage account. Additional Blob/Queue transactions, storage and worker execution are metered; Gemini generation remains chargeable. A job requests one image, and duplicate protection avoids intentional repeated generation while waiting. No dollar estimate or promise of zero cost is made. The existing authorized deployment identity must be able to deploy to this Function App; the existing storage connection must support Blob and Queue. If either is insufficient, stop rather than granting roles or creating new credentials. No permission expansion is authorized by this proposal.

## Backup and rollback

1. Re-read deployed files and compare with `deployed-source-sha256.json`; pause if another task has changed them.
2. Preserve the current complete deployment package/history before replacing it. `RollbackRuntimeSource.zip` additionally preserves the inspected function source, HTTP bindings, host config and original lock file; it is a reproducible runtime-source backup, not a byte-for-byte backup of the platform package. Restore its dependencies with the original lock file.
3. Prepare the candidate with `npm ci --omit=dev --ignore-scripts`. Do not include Azurite/test data in the deployed package.
4. On regressions restore the previous complete deployment package. Keep/remove the client opt-in define as appropriate to return to the current shared client path. Do not delete old caches or queue/job records as part of rollback. Generated requests already running cannot be undone.

## Passed locally

- 7 server core cases: cold/warm, simultaneous delivery, explicit bounded retry, enqueue interruption, new key, request budgets/validation, stale-worker recovery.
- 5 real local Azurite integration groups: text and image HTTP/queue/lease/cache/old-interface behavior, sanitized failure, text body timeout, image body timeout. **Gemini responses are stubs; these are not cloud cold/warm tests.**
- 13 Unity client protocol/parser checks: pending/warm, failed/missing/unavailable, old response, wrong job, empty result, cancellation, timeout, explicit retry.
- Existing 8 story lifecycle checks and final iOS script compilation.

## Repeatable live acceptance procedure

1. Deploy with the Unity define still disabled. Confirm both registered old HTTP functions remain available and the new queue trigger is running.
2. Log in using the existing development account. Submit a genuinely unique text prompt and unique image prompt (nonce in each canonical input), recording only job IDs/statuses/timings, never provider payload URLs or credentials. Verify each HTTP response completes below 10 seconds, observe queued/running/ready, and download the actual generated image.
3. Repeat exactly the same inputs; assert the same IDs and ready results without another provider invocation. Verify concurrency with duplicate requests for one input.
4. Verify invalid input, provider failure, explicit retry/cooldown, pending timeout and client cancellation/late response. Stop polling after navigation away. Do not induce unrelated production outages for fault tests; retain local fault fixtures where a cloud fault would be disruptive.
5. Exercise old-style text/image requests from the same registered functions to confirm MCombat compatibility.
6. Enable the local PocketStriker define only after service acceptance; run real battle preparation, story presentation and image/text/continue/result/home navigation, and repeat a battle. Confirm story ownership/cleanup, cache/variation and existing authored fallback remain correct. Cold-story timing may still exceed short battles; battle results must remain unblocked.
7. Re-run iOS compile and record exact cloud and client revisions. No app-store, Addressables or Git publication is included.

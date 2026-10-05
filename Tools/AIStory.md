# PocketStriker AI stories

PocketStriker uses the same PlayFab Title (`38054`) and registered Azure functions
(`generateGeminiText`, `generateGeminiImages`) as the local MCombat reference.
The shared package is unchanged. After the approved Azure deployment and real
service acceptance on 2026-10-01, the iOS project defines
`POCKETSTRIKER_QUEUED_STORIES` and uses its project-specific queue adapter.
Other build targets retain their existing define sets.

## Fixed cause and behavior

The former synchronous image call exceeded PlayFab's 10-second HTTP deadline.
The new envelope starts an Azure queue job and returns quickly; status calls do
not generate. Blob leases deduplicate concurrent work. A terminal failure can
retry only on an explicit new attempt after cooldown, with at most three total
generation attempts. Images are stored as Blob paths and signed URLs are freshly
issued when needed. Old MCombat request shapes, caches and registrations remain.
The old synchronous MCombat cold-image limit still applies to clients that have
not adopted the new protocol. Provider response-body timeouts are also corrected.

A follow-up real-account regression on 2026-10-02 reached the PlayFab SDK error
callback on its first request, then succeeded on a same-key replay. Its original
error code was not recorded, so the exact transient cause is not established.
The client now recovers known temporary transport errors and Azure HTTP/cold-start
timeouts with at most two resends across one job. Resends retain the identical
start input or status ID, remain inside the existing deadline, respect Retry-After,
and stop on cancellation. Permanent HTTP/auth errors and terminal worker failures
are not recovered this way; explicit generation retry retains its existing rules.
The live fixture now records safe status/timing/error categories without URLs.

The client stops polling on cancellation, bounds its owned pipeline to 100 seconds
and rejects stale results from another fight. The outer battle consumer is bounded
to 110 seconds (60 for the legacy path). AI stories remain optional: results never
wait for generation, and use an authored story or ordinary result if not ready.
Late/abandoned owned sprites and textures are released. Existing same-attempt
request reuse and explicit-next-attempt recovery are preserved.

The single-page prompt selects from 18 story subjects with shuffled twists and
tones, uses the current language, and keeps one variation seed per battle attempt.
A retry starts a new variation; repeated requests within an attempt share server
jobs. Ordinary Gemini Markdown JSON fences are handled, malformed output falls
back safely. Captions are capped at 200 characters; AI art is a static UI Image,
isolated from the authored background animation and per-letter text component.

Portrait illustrations now enlarge equally in both directions to fill the game
screen, with centered clipping at its edges on taller phones. Captions overlay
the image without reducing its size. Existing landscape illustrations stay fully
visible. The client configuration, prompt and image-job request specify `9:16`.
Read-only Azure inspection on 2026-10-05 found that the October 1 queue worker
still fixed image jobs to `16:9`, overriding the client request. The user approved
and we deployed the minimal shared-service update to `9:16`, preserving all legacy
function code and deployed indexing. Real PlayFab verification generated exactly
one image at **768×1376**, with one generation attempt; a warm replay reused its
job and image. All ten image-function HTTP responses completed in at most
**0.607 seconds**. The MCombat legacy request shape returned the unchanged cached
**1376×768** historical image when given `16:9`, and also returned the same new
portrait from its cache. No additional image or text was generated for these
compatibility checks. The live smoke now verifies actual sprite height exceeds
width. Sanitized deployment and acceptance records are under
`Logs/AIStory/PortraitDeployment`; the exact active source hashes are saved in
`Tools/AIStoryBackend/deployed-source-sha256.json`.

## Evidence and limits

Real development-account cold text and cold image tasks each generated once.
Cold-image pipeline/download with text cached: 13.982 seconds; warm pipeline:
1.433 seconds. Maximum observed HTTP callback across the first and replay runs:
0.748 seconds. Identical concurrent starts, cancellation/recovery, warm reuse,
actual 1376×768 download, invalid input and missing jobs passed. Legacy requests
also returned a visual story. The first cold text response exposed fenced JSON;
its paid result was reused after the parser fix. These are not Azure host
cold-start measurements or one uninterrupted all-cold pipeline measurement.

Two real-account natural battle fixtures passed image/caption/continue/result/home
and repeated entry, with no runtime errors. They use a clone of completed Quest 4,
22v1 and reduced enemy HP to shorten combat, and deliberately warm the story before
combat. No account replacement, story/result injection or remote reward. Three
separate isolated-account natural battles verify exception, permanent-wait and
empty-story fallback without blocking results. Provider outages and worker-crash
recovery use local fault fixtures; physical-device behavior and the separate full
MCombat client are not certified. A story can still finish after a short battle.

The full deployment/rollback/evidence details are in
[AIStoryBackend/README.md](AIStoryBackend/README.md). Raw provider/editor logs may
contain signed URLs: share sanitized reports and screenshots only.

## Validation

Use Unity 6000.5.1f1. Do not open a second editor on this checkout. Rendered
playmode checks require omitting `-quit` and `-nographics`.

```sh
# 21 local protocol/parser/recovery cases; no account/provider request.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerStoryJobValidation.ValidateBatch -logFile "$LOG_DIR/queued.log"
# 8 local lifecycle cases.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerAIStoryLifecycleValidation.ValidateBatch -logFile "$LOG_DIR/lifecycle.log"
# Real account, queued warm story, natural battle and native UI navigation.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerAIStoryLiveSmoke.StartQueuedBattleBatch -logFile "$LOG_DIR/live-private.log"
# Three injected-fault natural battles and five optional-request wrapper cases.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerCombatFlowSmoke.StartStoryFailureBatch -logFile "$LOG_DIR/failures.log"
Tools/validate_unity.sh compile ios
Tools/validate_unity.sh check ios
```

Reports are under `Logs/AIStory/{Queued,Lifecycle,Playmode,Live}` and
`Logs/Revival`. `StartQueuedServiceBatch` and `EnableClientBatch` are one-time,
guarded acceptance helpers: service acceptance requires the client define absent
and genuinely uncached input. Do not rerun it as a routine warm smoke or toggle a
deployed feature merely to satisfy the cold assertion. Use the archived service
report and preserved first-text evidence for this deployment.

Server tests: `cd Tools/AIStoryBackend && npm ci && npm test`. Optional Azurite
integration uses local Blob/Queue services and fake Gemini, not live generation.

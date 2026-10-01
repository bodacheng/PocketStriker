# PocketStriker AI stories

The client uses the same PlayFab Title (`38054`) and registered Azure functions
(`generateGeminiText`, `generateGeminiImages`) as the local MComat reference.
`GeminiConfig.asset` selects `gemini-3.1-flash-image`; `AIServiceConfig.asset`
requests one concise magic-stone adventure scene. The shared package is unchanged.

## Battle lifecycle

Story generation stays optional and runs in the background. Results use a story
only when it is already ready; otherwise the authored story or ordinary result
continues. The existing consumer has a 60-second realtime deadline and is cancelled
when leaving its scene. Late responses cannot replace another fight's story.

`PreparingProcess` marks an explicit new battle attempt. If a completed request
returned no story, this attempt clears that empty request even when the result
screen retains the same `FightInfo`. Ordinary reads and duplicate startup preloads
reuse the existing request. A pending request or ready story also remains reused.
There is no automatic resend loop: an Azure request can continue after PlayFab
has timed out, and server in-flight deduplication has not been established.

## Story presentation

AI artwork uses an ordinary UI Image in its own opaque panel. The legacy authored
background's sprite Animator previously replaced the generated illustration every
frame. AI captions use a separate Text without the legacy per-letter reveal
component, with wrapping, readable sizing, a localized heading and a full-panel
continue button. Finishing hides the complete story panel before result animation.
The authored fallback and result reward controls retain their existing paths.

## Validation

Use Unity 6000.5.1f1. Do not open a second editor on this checkout. The playmode
commands require rendering, so omit `-quit` and `-nographics`.

```sh
# Eight local request-lifecycle cases; no account/provider request.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerAIStoryLifecycleValidation.ValidateBatch \
  -logFile "$LOG_DIR/story-lifecycle.log"

# Actual production device login, Azure generation, completed Quest 4 fixture,
# natural battle, ready-cache reuse, real story clicks, result and home return.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerAIStoryLiveSmoke.StartAfterBatch \
  -logFile "$LOG_DIR/story-live.log"

# Existing isolated-account failure suite: exception, permanent wait and empty
# story in three natural Quest battles; five request wrapper cases.
Unity -batchmode -projectPath "$PROJECT" -buildTarget iOS \
  -executeMethod PocketStrikerCombatFlowSmoke.StartStoryFailureBatch \
  -logFile "$LOG_DIR/story-failures.log"

Tools/validate_unity.sh compile ios
```

Reports: `Logs/AIStory/Lifecycle/report.json`, `Logs/AIStory/Live/after/report.json`,
`Logs/AIStory/Playmode/report.json`, `Logs/Revival/compile-iOS-report.json`.
Live screenshots are 540×960 actual Game view captures. The live suite waits for
real generation in the test before launching a cloned, already-completed Quest 4
Group battle (22v1, reduced enemy HP). It uses the actual logged-in account, natural
combat and native EventSystem clicks; it neither injects a story/result nor awards
remote rewards. This demonstrates warm-cache integration, not unmodified cold
production timing or physical-device behavior. It counts provider responses without
copying their URLs or tokens into the report; raw editor logs should remain private.

## Remaining server issue (2026-10-01)

Both the old Imagen/three-page configuration and the updated single-page model
hit PlayFab's 10-second HTTP function deadline on a real cold image request.
The updated cold run returned no story after about 15.7 seconds. A later warm run
completed in about 2.7 seconds and passed image stability, text visibility, natural
battle, story completion and home return with nine pointer clicks and no errors.
This proves the service can populate and serve its cache; it does not fix the
first-request deadline.

The service needs inspection before changing its request/caching protocol. A
possible remedy is a bounded start/status job protocol with per-key in-flight
protection, so each HTTP response stays within PlayFab's limit while image work
continues independently. This has not been implemented or deployed. Automatic
approval review refused source/test links because they may contain signed access
credentials; explicit source-read authorization is pending. No Azure configuration,
function deployment, Addressables publication or remote Git push was performed.

Microsoft's documented HTTP limit:
https://learn.microsoft.com/en-us/xbox/playfab/live-service-management/service-gateway/automation/cloudscript-af/quickstart#execution-limits

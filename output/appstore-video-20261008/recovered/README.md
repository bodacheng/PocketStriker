# Recovered preview export pipeline

Recovered 2026-10-08 from archived chat “完善 PocketStriker 商店页面”
(thread 01a101c0-1820-74d6-96fe-7323a8d89091, host durable) and its
audio QA subagent 01a10656-b658-7444-b2c8-c7b5c6f2f0ad.

These are historical source files, not new game recordings. Assets/ was untouched.

- edit_real_clips.swift / binary: exact source assembled from historical sed
  lines 1–300 and 300–390 with the overlapping line removed.
- combined_preview_export.swift / binary: historical base plus the original
  BGM-mixing patch and original recorded-effects gain correction (0.5).
- combined_review_export.swift: same source, 3.5 Mbps review encode instead of 11 Mbps.
- inspect_frames.swift / binary: original frame extractor.
- inspect_combined_frames.swift / binary: original combined-frame positions,
  including the historical zero time-tolerance correction.
- audio_signal_probe.swift / binary: original audio PCM probe with original patch.
- combined-edit.original.json: exact original combined manifest from a historical
  cat command, including all source SHA256 values.
- battle-export.original.json and skill-export.original.json: exact earlier edits.
- historical-* commands preserve the original construction evidence.

Original combined timeline (seconds): duel 0–4.9, team 4.9–9.8, group 9.8–15.7,
skills 15.7–28.6. Skill source range starts at 1.5 and lasts 12.9 seconds.
Original output target: 886 × 1920, 30 fps, H.264 High Level 4.0, 11 Mbps,
AAC stereo 256 kbps / 48 kHz. Fit preserves UI with black borders.
Music gain 0.28, fade-in 0.15 seconds, fade-out 0.6 seconds; source starts at 0.

Music source currently exists and matches historical SHA256:
Assets/ExternalAssets/Audio/BGM/MIDI☆MUSIQ/BGM1_fin.wav
f4f6ecff5d001dfcc2242fd40b406d4ab2e82779aa00e2df27022b0f9a6a9cb0

All old raw clip paths were in the now-empty /tmp/pocketstriker-iphone-preview-20261004/.
The original manifest cannot be exported without those files. Create a fresh manifest
for newly recorded or downloaded clips with truthful provenance and current hashes.

The recovered exporters compile under the current host Swift toolchain.
The combined exporter --capabilities succeeds (no media generated).
Actual output requires its own media inspection and visual/audio QA.

Adaptation for the 2026-10-08 replacement preview:
Both combined exporter output roots now point at
/Users/daisei/PocketStriker/output/appstore-video-20261008.
Encoding and music rules are unchanged; both binaries were recompiled and their
capability probes passed.

inspect_media.swift / binary accepts video, a new QA output folder, and arbitrary
comma-separated times (or '-' for no frame extraction). It uses zero frame-time
tolerance and records requested/actual times, dimensions and each PNG hash.
media-probe.json includes source hash, real duration, video/audio formats,
avcC H.264 profile/level, declared FPS, compressed payload bitrate and decoded PCM
RMS/peak per second, silence and near-full-scale counts. It does not parse SPS
progressive flags or decide visual quality. It refuses to overwrite existing QA.

For the proposed 4.2/4.2/5.2/15 second ranges, request frames at:
0.1,4.1,4.3,8.3,8.5,13.5,13.7,17,21,25,28.4
The boundary before skills is 13.6 seconds. No new footage manifest was created.
PCM inspection was verified against the actual bundled BGM (88.6154s, stereo
44.1kHz, non-silent); see qa-bgm-probe/media-probe.json.

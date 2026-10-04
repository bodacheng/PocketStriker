#!/usr/bin/env python3
"""Compact two-page client download/loading report from current native evidence.

The first authoring invocation requires the current PDF skill marker immediately
before it, exactly once. This builder never launches Unity, edits production,
contacts a production endpoint, or saves to Library. --validate-only is read-only.
The native cache adapter stays closed until final real runner schema is bound.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import ipaddress
import json
import math
from pathlib import Path
import sys
from urllib.parse import urlparse

from build_menu_background_review_pdf import (
    EvidenceError, FONT_DEFAULT, PdfBuilder, load_json, passed_report,
    require, sha256, utc_value,
)

ROOT = Path(__file__).resolve().parents[2]
REVIEW = Path("Logs/ClientDownloadLoadingReview")
OUTPUT_NAME = "PocketStriker-Client-Download-Loading-20261003.pdf"
VIEWS = ("375x667", "390x844", "540x960", "768x1024")
CACHE_RUNS = ("Cold", "Restart1", "Restart2", "HashChange", "LargeConfirm", "FailureRecovery")
LOADING_FIXTURE = "Assets/Editor/PocketStrikerLoadingReviewValidation.cs"
LOADING_LAYOUT_FIXTURE_HASHES = (
    "fc2561884de4127489a6ee756ea0316b32e42a96a8376192bf3cefa9489adad0",
    "79fe1f2d339cf7ef597e364848035cd67e011826b899cc9e3f2d1ddd21772f5f",
)


def finite(value: object, label: str) -> float:
    require(isinstance(value, (int, float)) and not isinstance(value, bool)
            and math.isfinite(value), f"Missing finite actual measurement: {label}")
    return float(value)


def file_binding(root: Path, path: str, digest: str, size: int | None = None) -> Path:
    actual = root/path
    require(actual.is_file() and sha256(actual) == digest, f"Evidence/source bytes differ: {actual}")
    if size is not None:
        require(actual.stat().st_size == size, f"Evidence byte count differs: {actual}")
    return actual


def verify_sources(root: Path, review: Path) -> dict:
    paths = {"before": review/"Before/manifest.json", "protected": review/"Before/protected-artifacts.json",
             "packages": review/"Before/packages-protected.json",
             "standalone": review/"Download/standalone-test-receipt.json",
             "catalogs": review/"Download/saved-catalog-inspection.json",
             "freeze": review/"Download/production-freeze-and-scope.json",
             "loadingSource": review/"LoadingSourceAudit/source-audit.json"}
    reports = {k: load_json(p) for k, p in paths.items()}
    before, standalone = reports["before"], reports["standalone"]
    require(before.get("allCopiesVerified") is True and len(before.get("head", "")) == 40,
            "The current client-task Before/source freeze is incomplete")
    for item in before.get("files", []):
        file_binding(root, item["backupPath"], item["sha256"], item["bytes"])
    require(standalone.get("passed") is True and standalone.get("exitCode") == 0
            and standalone.get("checks") == 94
            and standalone.get("command") == ["python3", "Tools/Validation/validate_downloads.py"],
            "The current real Startup-code controlled test receipt is not complete")
    require(utc_value(before["utc"], "Before freeze") <= utc_value(standalone["startUtc"], "standalone start")
            <= utc_value(standalone["completedUtc"], "standalone finish"), "Standalone receipt predates this task")
    for path, digest in standalone["inputHashes"].items():
        file_binding(root, path, digest)
    require(reports["freeze"].get("productionFrozen") is True
            and reports["freeze"].get("acceptedHomeMismatches") == []
            and reports["freeze"].get("packagesMismatches") == [], "Download source freeze has unresolved changes")
    for item in reports["protected"]["files"]:
        file_binding(root, item["path"], item["sha256"], item["bytes"])
    require(len(reports["packages"]["files"]) == 877, "The protected Packages inventory differs")
    for item in reports["packages"]["files"]:
        file_binding(root, item["path"], item["sha256"], item["bytes"])
    source = reports["loadingSource"]
    require(source.get("status") == "passed" and source.get("productionWrites") is False
            and source.get("beforeManifestSHA256") == sha256(paths["before"]), "Loading source audit does not bind the current Before")
    prefab = source["prefab"]
    file_binding(root, prefab["path"], prefab["afterSHA256"])
    require(prefab.get("exactAuthorizedChangesOnly") is True and prefab.get("allRectTransformsUnchanged") == 7
            and prefab.get("allTextComponentsUnchanged") == 2, "Loading prefab changed outside its background")
    protected_menu = source["protectedAcceptedMenuInputs"]
    require(protected_menu.get("count") == 32 and protected_menu.get("allUnchanged") is True,
            "The accepted six-color MainMenu inputs changed")
    for item in protected_menu["checks"]:
        require(item.get("unchanged") is True and item["beforeSHA256"] == item["currentSHA256"], "Accepted MainMenu input changed")
        file_binding(root, item["path"], item["currentSHA256"])
    for item in source["loadingNeighbors"]["checks"]:
        require(item.get("unchanged") is True, "A neighboring loading input changed")
        file_binding(root, item["path"], item["currentSHA256"])
    catalogs = reports["catalogs"].get("catalogs", [])
    require(len(catalogs) == 2, "Exactly the two saved local catalog snapshots are required")
    sets = []
    for catalog in catalogs:
        file_binding(root, catalog["path"], catalog["sha256"])
        keys = {tuple(key) for key in catalog["requiredRemoteCacheKeys"]}
        require(len(keys) == len(catalog["requiredRemoteCacheKeys"]) == catalog["requiredRemoteBundleCount"] == 384,
                "Saved required-remote cache-key inventory is incomplete/duplicate")
        require(catalog.get("invalidRemoteHashes") == catalog.get("duplicateRemoteCacheKeys") == [],
                "A saved catalog has invalid or duplicate remote cache keys")
        sets.append(keys)
    counts = (len(sets[0]&sets[1]), len(sets[0]-sets[1]), len(sets[1]-sets[0]))
    reported = reports["catalogs"]["savedCatalogCacheKeyComparison"]
    require(counts == (173, 211, 211) and counts == (reported["commonPairs"], reported["archiveOnlyPairs"], reported["currentOnlyPairs"])
            and reports["catalogs"].get("savedCatalogRequiredRemoteCacheKeysEqual") is False,
            "Saved catalog cache-key comparison is not the actual set difference")
    return {"paths": paths, "reports": reports, "head": before["head"], "catalogComparison": counts}


def verify_loading(root: Path, review: Path) -> dict:
    from PIL import Image

    path = review/"Loading/Runtime/report.json"
    r = load_json(path)
    passed_report(r, "Current native loading UI", complete=True)
    require(r.get("sourceAssetsUnchanged") is True and r.get("externalServicesIsolated") is True,
            "Native Loading validation changed source or escaped isolated services")
    counts = {"viewportCases": 4, "startupDownloadCases": 4, "instructionCases": 4,
              "closeReopenCases": 4, "resizeCases": 4, "actualSingleSceneLoads": 8,
              "nativeReturnClicks": 4, "grayscaleChecks": 4, "progressAnimationChecks": 20,
              "textLayoutChecks": 24, "backgroundScrollerChecks": 24}
    require(all(r.get(k) == v for k, v in counts.items()), "Native Loading UI coverage is incomplete")
    cases_list = r.get("cases", [])
    cases = {c["viewport"]: c for c in cases_list}
    require(len(cases_list) == len(cases) == 4 and set(cases) == set(VIEWS), "Native Loading viewport cases differ")
    frames = {}
    inventory = {}
    source_png_sha = load_json(review/"LoadingSourceAudit/source-audit.json")["sixRuntimePNG"]["commonSHA256"]
    for view, case in cases.items():
        width, height = map(int, view.split("x"))
        expected = {"x": (1-width/height*1670/942)/2, "y": 1.0,
                    "width": width/height*1670/942, "height": -1.0}
        require(case.get("staticGradient") is True and case.get("verticalFlip") is True
                and case.get("backgroundCoversViewport") is True and case.get("nativeReturnRequested") is True,
                "Native Neutral Loading background or return callback assertion failed")
        require(case.get("shader") == "PocketStriker/UI/StaticPixelGradient"
                and (case.get("filterMode"), case.get("wrapU"), case.get("wrapV")) == ("Point", "Repeat", "Clamp"),
                "Native Neutral material/sampler differs")
        require(case.get("materialPath") == "Assets/MainSceneSystem/StaticPixelGradientNeutral.mat"
                and case.get("sourcePngSha256") == source_png_sha, "Loading uses another material or image source")
        require((case.get("textureWidth"), case.get("textureHeight")) == (942, 1670), "Native Neutral source dimensions differ")
        for key, value in expected.items():
            require(abs(finite(case["observedUv"][key], "observed static UV")-value) <= .00001,
                    "Loading does not retain centered full-height vertically flipped sampling")
        for key in ("downloadScreenshot", "instructionScreenshot", "reopenedScreenshot", "menuReloadScreenshot", "returnedScreenshot"):
            original = Path(case[key])
            canonical = review/"Loading/Runtime"/view/original.name
            require(canonical.is_file(), f"Current native Loading frame is missing: {canonical}")
            with Image.open(canonical) as image:
                require(image.size == (width, height), f"Native capture dimensions differ: {canonical}")
            require(original.is_file() and sha256(original) == sha256(canonical), "Archived capture differs from actual native original")
            inventory[str(canonical.relative_to(root))] = sha256(canonical)
            if key in ("downloadScreenshot", "instructionScreenshot"):
                frames[(view, key)] = canonical
    require(len(inventory) == 20 and set(inventory) == {str(p.relative_to(root)) for p in (review/"Loading/Runtime").rglob("*.png")},
            "Native Loading inventory differs from four viewports times five captures")
    observations = r.get("observations", [])
    require(len(observations) == 24, "All 24 natural Loading observations are required")
    purposes = ("startup-download-opaque-black", "initial-battle-loading", "live-resize", "close-destroy-reopen",
                "actual-mainmenu-single-load", "return-callback-and-actual-startup-single-load")
    require({o["purpose"] for o in observations} == set(purposes)
            and all(sum(o["purpose"] == p for o in observations) == 4 for p in purposes),
            "Loading purpose coverage differs from six natural observations per viewport")
    for purpose in purposes:
        if purpose != "live-resize":
            require({o["viewport"] for o in observations if o["purpose"] == purpose} == set(VIEWS),
                    "A natural loading/navigation purpose lacks a viewport")
    max_delta = 0.0
    stationary_frames = 0
    for o in observations:
        require(o.get("percentageMatchesSlider") is True and o.get("elapsedRealtime", 0) >= 1.2 and len(o.get("frames", [])) >= 2,
                "Loading observation lacks real frames, duration or correct live progress")
        require(utc_value(r["utcTime"], "Loading native start") <= utc_value(o["startedUtc"], "Loading observation start")
                <= utc_value(o["finishedUtc"], "Loading observation finish") <= utc_value(r["finishedUtc"], "Loading native finish"),
                "Loading observation time is outside its actual native run")
        require(o.get("instruction") == (o["purpose"] != "startup-download-opaque-black"),
                "Black startup and actual instruction observations are conflated")
        if o["purpose"] != "live-resize":
            require(o.get("progressAdvanced") is True and o["endSlider"] > o["startSlider"]
                    and abs(o["endSlider"]-.8) < .01 and o["endPercentage"] == 80 and o["percentageChanges"] >= 2,
                    "Natural Loading progress did not advance to the actual 80% tween target")
        before = o["beforeUv"]
        last_unity_frame, last_elapsed = -1, -1.0
        for frame in o["frames"]:
            require(frame["unityFrame"] > last_unity_frame and frame["elapsedRealtime"] >= last_elapsed,
                    "Natural Loading frames are unordered or duplicated")
            last_unity_frame, last_elapsed = frame["unityFrame"], frame["elapsedRealtime"]
            require(frame.get("scrollerEnabled") is False, "Production Loading background scroller became enabled")
            delta = max(abs(finite(frame["uvRect"][k], "frame UV")-finite(before[k], "before UV"))
                        for k in ("x", "y", "width", "height"))
            max_delta = max(max_delta, delta)
            require(delta <= .000001, "Loading background moved during natural progress animation")
            require(abs(frame["percentage"]-math.floor(frame["slider"]*100)) <= 1, "Native percentage text differs from actual slider")
        require(max(abs(o["afterUv"][k]-before[k]) for k in ("x", "y", "width", "height")) <= .000001,
                "Natural Loading finish reset the static background UV")
        if o["instruction"]:
            stationary_frames += len(o["frames"])
    require(abs(max_delta-r["maximumObservedUvDelta"]) <= .000001, "Reported maximum Loading UV drift differs from raw frames")
    require(r.get("stationaryFrames") == stationary_frames, "Natural stationary frame count differs from raw observations")
    text = r.get("textMeasurements", [])
    require(len(text) == 72 and all(t.get("renderedCharactersComplete") is True and t.get("renderedGeometryComplete") is True
                and t["renderedVisibleCharacters"] == t["completeVisibleCharacters"] == t["cachedVisibleCharacters"] > 0
                and t["renderedVertices"] == t["completeVertices"] == t["cachedVertices"] >= 4
                and finite(t["preferredHeight"], "actual preferred text height") <= t["rect"]["height"]+.6 for t in text),
            "Loading text lacks complete actual rendered/cached glyph geometry and preferred-height checks")
    transitions = r.get("sceneTransitions", [])
    require(len(transitions) == 8 and all(t.get("actualSingleLoad") is True and t["beforeHandle"] != t["afterHandle"] for t in transitions),
            "Loading does not contain eight actual isolated Single scene loads")
    return {"report": r, "path": path, "frames": frames, "inventory": inventory, "maximumUvDelta": max_delta}


def verify_native_cache_and_compile(root: Path, review: Path, source: dict, loading: dict) -> dict:
    from PIL import Image

    paths = {"parity": review/"After/source-parity.json", "bundles": review/"Cache/Bundles/bundle-manifest.json"}
    parity = load_json(paths["parity"])
    passed_report(parity, "Current execution source parity", complete=True)
    require(parity.get("sourceBaseHead") == source["head"] and parity.get("count") == 411
            and len(parity.get("files", [])) == 411
            and len({p["path"] for p in parity["files"]}) == 411, "The final unique 411 execution inputs are incomplete")
    clone = Path(parity["clonePath"]).resolve()
    require(Path(parity["projectPath"]).resolve() == root, "Final parity is for another root project")
    for item in parity["files"]:
        require(item.get("matches") is True and item["sha256"] == item["rootSha256"] == item["cloneSha256"],
                "An execution input differs")
        for base in (root, clone, review/"After/Source"):
            file_binding(base, item["path"], item["sha256"])
    runs = {}
    final_map = {i["path"]: i["sha256"] for i in parity["files"]}
    historical_bindings = {}

    def stage_parity(name: str, receipt_path: Path) -> Path:
        digest = load_json(receipt_path).get("sourceParityManifestSha256")
        if digest == sha256(paths["parity"]):
            return paths["parity"]
        require(name in ("Bundles", *CACHE_RUNS), "Loading and compile must use the final actual fixture freeze")
        matches = [p for p in (review/"After/ParityHistory").glob("*/source-parity.json") if sha256(p) == digest]
        require(len(matches) == 1, f"Stage lacks its exact actual archived source manifest: {name}")
        path = matches[0]; old = load_json(path)
        passed_report(old, name+" actual archived source parity", complete=True)
        old_map = {i["path"]: i["sha256"] for i in old["files"]}
        require(old.get("sourceBaseHead") == source["head"] and old.get("count") == len(old_map) == len(old["files"]) == 411
                and set(old_map) == set(final_map), "Archived actual 411 source inventory differs")
        changed = {p for p in old_map if old_map[p] != final_map[p]}
        allowed = {LOADING_FIXTURE}
        if name == "Bundles":
            allowed.add("Assets/Editor/PocketStrikerDownloadCacheValidation.cs")
        require(changed <= allowed and all(i.get("matches") is True and i["sha256"] == i["rootSha256"] == i["cloneSha256"]
                                          for i in old["files"]), "An actual historical stage changed outside declared Editor fixtures")
        if LOADING_FIXTURE in changed:
            require((old_map[LOADING_FIXTURE], final_map[LOADING_FIXTURE]) == LOADING_LAYOUT_FIXTURE_HASHES,
                    "Loading historical fixture is not the independently reviewed layout/glyph-only correction")
            file_binding(path.parent, LOADING_FIXTURE, old_map[LOADING_FIXTURE])
            paths[name+"LoadingFixture"] = path.parent/LOADING_FIXTURE
        # Every production input must be identical across the actual historical
        # execution and final freeze. Only the named Editor fixtures may differ.
        require(all(old_map[p] == final_map[p] for p in old_map if not p.startswith("Assets/Editor/")),
                "Production input differs between executed stage and final source freeze")
        paths[name+"Parity"] = path
        historical_bindings[name] = {"path": str(path), "sha256": sha256(path), "changedEditorInputs": sorted(changed)}
        return path

    def receipt(name: str, report_path: Path, receipt_path: Path, native: dict, earliest: datetime,
                parity_path: Path | None = None) -> dict:
        value = load_json(receipt_path)
        bound_parity = parity_path or paths["parity"]
        require(value.get("name") == name and value.get("status") == "finished" and value.get("exitCode") == 0
                and value.get("sourceBaseHead") == source["head"]
                and value.get("sourceParityManifestSha256") == sha256(bound_parity)
                and value.get("nativeReportSha256") == sha256(report_path)
                and value.get("targetAbsentBeforeRun") is True and value.get("sourceUnchangedAfterRun") is True,
                f"No successful fresh source-bound actual runner: {name}")
        start = utc_value(value.get("startedUtc"), name+" start")
        end = utc_value(value.get("finishedUtc"), name+" finish")
        require(earliest <= start <= end and start <= utc_value(value.get("nativeReportModifiedUtc"), name+" report mtime") <= end,
                f"Actual native report was not newly written in this run: {name}")
        if native.get("utcTime"):
            require(start <= utc_value(native["utcTime"], name+" report UTC") <= end, f"Actual report UTC is outside its run: {name}")
        if native.get("finishedUtc"):
            require(start <= utc_value(native["finishedUtc"], name+" native finish") <= end, f"Actual native finish is outside its run: {name}")
        original = Path(value["nativeReportPath"])
        require(original.is_file() and sha256(original) == sha256(report_path), "Canonical native report differs from the actual runner target")
        command = value.get("command", [])
        require(isinstance(command, list) and command, "Missing actual executable command")
        if name == "Compile":
            require(len(command) == 2 and Path(command[0]).name == "validate_unity.sh" and command[1] == "compile",
                    "Fresh compilation did not execute the actual player-script validator")
        else:
            method = "PocketStrikerDownloadCacheValidation.BuildReviewBundlesBatch" if name == "Bundles" else (
                "PocketStrikerLoadingReviewValidation.StartBatch" if name == "Loading" else "PocketStrikerDownloadCacheValidation.StartBatch")
            require("-executeMethod" in command and method in command and "-projectPath" in command
                    and Path(command[command.index("-projectPath")+1]).resolve() == clone,
                    f"Actual fixture method/project differs: {name}")
        paths[name+"Receipt"] = receipt_path
        runs[name] = value
        return value

    manifest = load_json(paths["bundles"])
    passed_report(manifest, "Actual diagnostic bundle build", complete=True)
    require(manifest.get("buildTarget") == "StandaloneOSX" and manifest.get("unityVersion") == "6000.5.1f1"
            and Path(manifest["projectPath"]).resolve() == clone, "Diagnostic bundles are not the actual macOS Editor inputs")
    bundle_list = manifest.get("bundles", [])
    bundles = {b["variant"]: b for b in bundle_list}
    require(len(bundle_list) == len(bundles) == 4 and set(bundles) == {"small-v1", "small-v2", "large", "failure"},
            "Four actual diagnostic variants are required")
    for variant, bundle in bundles.items():
        file_binding(root, bundle["bundlePath"], bundle["fileSha256"], bundle["bundleBytes"])
        require(isinstance(bundle.get("crc"), int) and bundle["crc"] > 0 and len(bundle.get("hash", "")) == 32,
                "Actual diagnostic bundle hash or CRC is absent")
        require((bundle["bundleBytes"] > 65536) if variant == "large" else 0 < bundle["bundleBytes"] <= 65536,
                "Actual bundle bytes do not exercise the claimed policy branch")
    initial = utc_value(parity["utcTime"], "final source parity")
    build_receipt_path = review/"Cache/Bundles/Execution/run-receipt.json"
    build_receipt = load_json(build_receipt_path)
    build_parity_path = stage_parity("Bundles", build_receipt_path)
    build_source_scope = "diagnostic build and final validation use the same 411 inputs"
    if build_parity_path != paths["parity"]:
        old = load_json(build_parity_path)
        passed_report(old, "Actual diagnostic-build archived source parity", complete=True)
        old_map = {i["path"]: i["sha256"] for i in old["files"]}
        new_map = {i["path"]: i["sha256"] for i in parity["files"]}
        fixture = "Assets/Editor/PocketStrikerDownloadCacheValidation.cs"
        require(old.get("sourceBaseHead") == source["head"] and old.get("count") == 411
                and set(old_map) == set(new_map)
                and {p for p in old_map if old_map[p] != new_map[p]} <= {fixture, LOADING_FIXTURE}
                and old_map[fixture] != new_map[fixture],
                "Diagnostic build differs outside its declared Editor fixtures")
        paths["diagnosticBuildParity"] = build_parity_path
        paths["diagnosticBuildFixture"] = build_parity_path.parent/fixture
        file_binding(build_parity_path.parent, fixture, old_map[fixture])
        previous_text = paths["diagnosticBuildFixture"].read_text()
        final_text = (root/fixture).read_text()
        old_initialization = '            var initialization = Addressables.InitializeAsync(false); await initialization.Task; Require(initialization.Status == AsyncOperationStatus.Succeeded, "Local Addressables initialization failed."); Addressables.Release(initialization);'
        new_initialization = '\n'.join([
            '            // A local prefab may already have started auto-releasing initialization.',
            '            // Hold an explicit reference across its Task continuation.',
            '            var initialization = Addressables.ResourceManager.Acquire(Addressables.InitializeAsync(false));',
            '            await initialization.Task; Require(initialization.Status == AsyncOperationStatus.Succeeded, "Local Addressables initialization failed."); Addressables.Release(initialization);'])
        require(previous_text.count(old_initialization) == 1
                and previous_text.replace(old_initialization, new_initialization) == final_text,
                "Archived fixture difference exceeds the exact Editor initialization-handle correction")
        method = lambda text: text.split("    public static void BuildReviewBundlesBatch()", 1)[1].split("    public static void StartBatch()", 1)[0]
        require(method(previous_text) == method(final_text), "Diagnostic bundle-build method changed")
        build_source_scope = "each run binds its actual 411 manifest; diagnostic build has only the reviewed Editor initialization handle and Loading glyph-validation differences; exact bundle-build method and all production inputs unchanged"
    build_initial = utc_value(load_json(build_parity_path)["utcTime"], "actual build source parity")
    build_run = receipt("Bundles", paths["bundles"], build_receipt_path, manifest, build_initial, build_parity_path)
    previous = utc_value(build_run["finishedUtc"], "diagnostic build finish")
    reports, http_reports, screenshots, process_ids, cache_paths = {}, {}, {}, set(), set()
    for stage in CACHE_RUNS:
        paths[stage] = review/"Cache"/stage/"report.json"
        r = load_json(paths[stage]); reports[stage] = r
        passed_report(r, "Current actual provider/cache "+stage, complete=True)
        stage_receipt_path = review/"Cache"/stage/"Execution/run-receipt.json"
        actual_parity_path = stage_parity(stage, stage_receipt_path)
        stage_initial = utc_value(load_json(actual_parity_path)["utcTime"], stage+" actual source parity")
        run = receipt(stage, paths[stage], stage_receipt_path, r, max(stage_initial, previous), actual_parity_path)
        previous = utc_value(run["finishedUtc"], stage+" finish")
        require(r.get("stage") == stage and r.get("unityVersion") == manifest["unityVersion"]
                and Path(r["projectPath"]).resolve() == clone and isinstance(r.get("processId"), int) and r["processId"] > 0,
                "Actual fresh Editor process identity differs")
        process_ids.add(r["processId"]); cache_paths.add(str(Path(r["cachePath"]).resolve()))
        require(all(r.get(k) is True for k in ("cacheOwned", "externalServicesIsolated", "startupGoGated", "sourceAssetsUnchanged", "cachedAfter"))
                and r.get("requiredAfter") == 0 and r.get("automaticLimitBytes") == 65536
                and r.get("provider") == "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider",
                "Actual provider/cache/startup scope assertions are incomplete")
        variant = {"HashChange": "small-v2", "LargeConfirm": "large", "FailureRecovery": "failure"}.get(stage, "small-v1")
        b = bundles[variant]
        require((r["bundleName"], r["hash"], r["payloadSha256"], r["observedPayloadSha256"])
                == (b["bundleName"], b["hash"], b["payloadSha256"], b["payloadSha256"]), "Actual loaded payload/key differs from the built diagnostic bundle")
        warm = stage in ("Restart1", "Restart2")
        require(r.get("cachedBefore") == warm and r.get("requiredBefore") == (0 if warm else b["bundleBytes"]),
                "Actual cache validity and remaining bytes differ from the intended cold/warm stage")
        require(r.get("confirmations") == (1 if stage == "LargeConfirm" else 0)
                and r.get("warnings") == (1 if stage == "FailureRecovery" else 0)
                and r.get("nativePointerClicks") == (1 if stage in ("LargeConfirm", "FailureRecovery") else 0)
                and r.get("noConfirmation") == (stage != "LargeConfirm"), "Actual automatic/consent/retry route differs")
        if stage == "HashChange":
            require(r.get("previousHash") == bundles["small-v1"]["hash"] and r.get("previousHashStillCached") is True
                    and r["hash"] != r["previousHash"] and r["bundleName"] == bundles["small-v1"]["bundleName"],
                    "Same-name new-hash stage lacks the original-cache-before-download proof")
        if stage == "FailureRecovery":
            require(r.get("retrySceneReloaded") is True and r.get("sceneHandleBefore") != r.get("sceneHandleAfter")
                    and r.get("expectedFailureLogs", 0) > 0 and r.get("controlledFailures"), "Actual 503 warning and native retry Scene reload are unproven")
        ready = [v for v in r.get("cacheReadiness", []) if v.get("purpose") == "before-production-PrepareStartup-size-and-download"]
        require(len(ready) == 1 and ready[0].get("cachingReady") is True and ready[0].get("ownedCacheReady") is True
                and ready[0].get("versionCached") == warm and ready[0].get("requiredBytes") == r["requiredBefore"],
                "Fixture cache-ready gate and actual pre-startup size are unproven")
        env = run.get("environment", {})
        require(env.get("POCKETSTRIKER_CACHE_STAGE") == stage
                and Path(env["POCKETSTRIKER_CACHE_DATA_ROOT"]).resolve()/"cache" == Path(r["cachePath"]).resolve(),
                "Actual stage/cache path differs from its runner environment")
        start, end = utc_value(run["startedUtc"], stage), utc_value(run["finishedUtc"], stage)
        for request in r.get("requests", []):
            uri = urlparse(request["url"])
            require(uri.hostname == "localhost" or ipaddress.ip_address(uri.hostname).is_loopback, "Provider constructed a non-loopback request")
            require(start <= utc_value(request["utc"], "provider request construction") <= end, "Provider request time is outside its stage")
        paths[stage+"Http"] = review/"Cache"/stage/"Execution/http-requests.json"
        http = load_json(paths[stage+"Http"]); http_reports[stage] = http
        bundle_requests = [q for q in http.get("requests", []) if q.get("path", "").startswith("/bundles/")]
        require(http.get("bundleRequestCount") == len(bundle_requests), "Actual HTTP server bundle count differs from its raw request list")
        for q in http.get("requests", []):
            require(start <= utc_value(q["startedUtc"], "actual HTTP start") <= utc_value(q["finishedUtc"], "actual HTTP finish") <= end,
                    "Server request belongs to another process interval")
            peer = q["peer"][0] if isinstance(q["peer"], list) else q["peer"]
            require(ipaddress.ip_address(peer).is_loopback, "Actual server peer is not loopback")
        if warm:
            # A cached payload load can construct a UnityWebRequest after the
            # startup zero-byte assertion. Only server traffic proves network.
            require(not bundle_requests, "Actual same-key warm restart caused network bundle traffic")
        else:
            require(r.get("requests") and bundle_requests and all(q["path"] == f"/bundles/{variant}/{b['bundleName']}" for q in bundle_requests),
                    "Actual HTTP requests are absent or belong to another diagnostic bundle")
            require(any(q.get("status") == 200 and q.get("bodyBytesSent") == b["bundleBytes"] for q in bundle_requests),
                    "Actual server did not transmit the complete successful diagnostic bundle")
        if stage == "FailureRecovery":
            require(any(q.get("status") == 503 for q in bundle_requests), "Controlled failure lacks a real HTTP503 request")
            failures = [utc_value(q["startedUtc"], "503 request") for q in bundle_requests if q["status"] == 503]
            successes = [utc_value(q["startedUtc"], "recovery request") for q in bundle_requests if q["status"] == 200]
            require(min(successes) > min(failures), "Actual successful recovery does not follow the controlled failure")
        stage_images = {}
        for recorded in r.get("screenshots", []):
            original = Path(recorded); canonical = review/"Cache"/stage/original.name
            require(original.is_file() and canonical.is_file() and sha256(original) == sha256(canonical), "Actual cache-stage native image bytes differ")
            with Image.open(canonical) as image:
                require(image.size == (375, 667), "Actual cache stage native viewport differs")
            stage_images[original.stem] = canonical
        require("startup-complete" in stage_images, "Completed native startup image is missing")
        if stage == "LargeConfirm":
            require("large-confirm" in stage_images, "Actual large-consent native screenshot is missing")
        if stage == "FailureRecovery":
            require("offline-warning" in stage_images, "Actual recoverable-warning native screenshot is missing")
        if stage == "Cold":
            qualifying = [f for f in r.get("frames", []) if f.get("progressLayer") is True and f.get("downloadedBytes", 0) > 0 and 0 <= f.get("progress", -1) < 1]
            require("actual-auto-download-progress" in stage_images and qualifying,
                    "Actual cold startup progress image lacks real downloaded-byte/progress evidence")
        screenshots[stage] = stage_images
    require(len(process_ids) == 6 and len(cache_paths) == 1, "Six stages did not run in distinct processes with the same dedicated cache")
    cold = reports["Cold"]
    for stage in ("Restart1", "Restart2"):
        require((reports[stage]["bundleName"], reports[stage]["hash"]) == (cold["bundleName"], cold["hash"]), "Warm stage did not reuse exactly the Cold cache key")
    load_run = receipt("Loading", loading["path"], review/"Loading/Runtime/Execution/run-receipt.json", loading["report"], initial)
    paths["compile"] = review/"Regression/compile-iOS-report.json"
    compiled = load_json(paths["compile"]); passed_report(compiled, "Current iOS PlayerScripts compile")
    require(compiled.get("unityVersion") == compiled.get("expectedUnityVersion") == manifest["unityVersion"]
            and compiled.get("activeBuildTarget") == "iOS" and compiled.get("portraitOnly") is True,
            "Fresh compile is not the actual expected iOS Portrait PlayerScripts validator")
    latest = max(previous, utc_value(load_run["finishedUtc"], "loading finish"))
    receipt("Compile", paths["compile"], review/"Regression/Execution/run-receipt.json", compiled, latest)
    summary = (f"6独立Editor阶段；冷启动{cold['requiredBefore']}B({cold['requiredBefore']/1024:.2f}KiB)自动下载；同键重启2次均剩余0/真实HTTP bundle请求0；新hash重新下载；大包Yes确认；503后原生重试恢复")
    times = f"Cache: {runs['Cold']['startedUtc']} → {runs['FailureRecovery']['finishedUtc']}\nLoading: {load_run['startedUtc']} → {load_run['finishedUtc']}\nCompile: {runs['Compile']['startedUtc']} → {runs['Compile']['finishedUtc']}"
    return {"paths": paths, "runs": runs, "cacheReports": reports, "httpReports": http_reports,
            "cacheSummary": summary, "compileSummary": f"{compiled['unityVersion']} / iOS / Portrait，实际PlayerScripts编译通过",
            "executionSummary": times, "automaticProgressScreenshot": screenshots["Cold"]["actual-auto-download-progress"],
            "bundleSourceScope": build_source_scope,
            "historicalSourceBindings": historical_bindings,
            "cacheImageBindings": {stage: {kind: {"path": str(path), "sha256": sha256(path)} for kind, path in images.items()}
                                   for stage, images in screenshots.items()}}


def verify(root: Path, *, sources_only: bool = False) -> dict:
    review = root/REVIEW
    source = verify_sources(root, review)
    if sources_only:
        return {"root": root, "review": review, "source": source}
    loading = verify_loading(root, review)
    native = verify_native_cache_and_compile(root, review, source, loading)
    return {"root": root, "review": review, "source": source, "loading": loading, "native": native}


class Builder(PdfBuilder):
    def page(self, title: str, subtitle: str) -> None:
        from reportlab.lib.colors import HexColor
        if self.page_no:
            self.canvas.showPage()
        self.page_no += 1
        self.canvas.setFillColor(HexColor("#FAF8F3"))
        self.canvas.rect(0, 0, self.WIDTH, self.HEIGHT, fill=1, stroke=0)
        self.text("POCKETSTRIKER / DOWNLOAD + LOADING", 34, 770, size=8, color="#647383")
        self.text(title, 34, 746, size=20)
        self.paragraph(subtitle, 34, 729, 544, size=9.2, bottom=700)
        self.canvas.setStrokeColor(HexColor("#D8D4CB"))
        self.canvas.line(34, 46, 578, 46)
        self.text("本轮原生 Editor 截图 | 受控离线验收，非设备/CDN认证", 34, 29, size=8, color="#6F7780")
        self.text(f"{self.page_no} / 2", 552, 29, size=8, color="#6F7780")

    def finish(self) -> None:
        require(self.page_no == 2, "Client review must have exactly two pages")
        self.canvas.save()


def build(e: dict, output: Path, font: Path, quality: int) -> None:
    b = Builder(output, font, quality)
    b.canvas.setTitle("PocketStriker 客户端下载与静态加载界面验收")
    b.page("客户端下载与静止 Loading", "本轮原生完整截图：真实localhost自动小包进度，以及黑色下载图层、中性黑灰战斗说明的离线界面回归。")
    centers = (124.667, 306.0, 487.333)
    pictures = [(e["native"]["automaticProgressScreenshot"], "375x667", "真实自动下载 / localhost", 0, 430),
                (e["loading"]["frames"][("375x667", "downloadScreenshot")], "375x667", "黑色ProgressLayer / 手机", 1, 430),
                (e["loading"]["frames"][("375x667", "instructionScreenshot")], "375x667", "静止战斗Loading / 手机", 2, 430),
                (e["loading"]["frames"][("768x1024", "downloadScreenshot")], "768x1024", "黑色ProgressLayer / 平板", 0, 138),
                (e["loading"]["frames"][("768x1024", "instructionScreenshot")], "768x1024", "静止战斗Loading / 平板", 1, 138)]
    for path, view, label, column, y in pictures:
        vw, vh = map(int, view.split("x")); h = 230; w = h*vw/vh
        b.text(label, centers[column]-82, y+244, size=9)
        b.image(path, centers[column]-w/2, y, w, h)
    b.text("截图来源", 406, 380, size=11)
    b.paragraph("左上图来自真实StartUp代码与AssetBundleProvider，仅下载字节>0、进度<1时触发。其余图使用真实Loading预制体、fixture模拟进度；两种证据分开记录。", 406, 361, 167, size=9)
    b.paragraph("显示完整原生画面，无拼图改绘或裁切；截图请求与采样帧未声称原子同步。真实下载使用本机macOS诊断包，不是用户20.2KB原资源。", 406, 249, 167, size=8.7)
    b.paragraph("375x667手机 / 768x1024平板展示；390x844、540x960另由本轮真实Loading回归覆盖。所有画面来自本轮，未改首页。", 34, 104, 544, size=8.7, bottom=55)
    b.page("两项改动 / 真实测试与边界", "运行结果来自本轮受控资源、真实 UI 和新编译；尚未识别用户 iPhone 每次提示20.2KB的具体缓存未命中原因。")
    y = b.paragraph("下载提示：原启动流程对任意非零剩余量弹确认；现在0字节跳过，1-64KiB直接进入原下载/进度/重试流程，超过64KiB继续确认。必要小更新仍实际下载，失败仍保留原可恢复警告。", 34, 692, 544, size=10)-12
    y = b.paragraph("加载背景：UnitInstruction BG原来引用Neutral PNG，但无专用材质且自身OffsetScrolling开启。现在使用Neutral材质、static aspect全高纵向翻转、关闭同对象滚动；黑色startup下载进度与文本/布局保留。两项修复涉及3个生产文件。", 34, y, 544, size=10)-15
    r = e["loading"]["report"]
    rows = [("真实Startup代码 / 离线替身", "94检查；0/64KiB/超过阈值，三语言进度、已有缓存、小更新、剩余量重试与持续失败恢复。UI/Addressables为受控doubles"),
            ("真实provider / 新Editor进程", e["native"]["cacheSummary"]),
            ("本轮Loading原生回归", f"{r['viewportCases']}比例 / 20截图 / 24自然观察 / {r['progressAnimationChecks']}进度动画；UV最大变化{e['loading']['maximumUvDelta']:.6g}"),
            ("重建、尺寸与返回回调", f"{r['closeReopenCases']}销毁重开 / {r['resizeCases']}live resize / {r['actualSingleSceneLoads']}隔离Single加载 / {r['nativeReturnClicks']}原生Return回调"),
            ("保存catalog的真实差异", "两份各384所需远程缓存键；173共同，另外各211不同。不同build快照，不能称同cache keys或确定设备20.2KB原因"),
            ("本轮新iOS脚本编译", e["native"]["compileSummary"])]
    y = b.table(rows, y, size=8.9)-14
    b.text("实际执行时间 (UTC)", 34, y, size=11)
    y = b.paragraph(e["native"]["executionSummary"], 34, y-8, 544, size=8)-14
    b.text("未验证范围", 34, y, size=11)
    y = b.paragraph("实际provider使用localhost、macOS诊断包及隔离临时cache，并等待缓存ready；跨新Editor持久性仅证明该案例，不代替iPhone/CDN，也不诊断生产缓存竞态。大包原生仅Yes，取消由94项doubles覆盖。启动Go在账户初始化前gated。Loading的Return回调后由fixture请求隔离场景切换，未执行真实战斗准备、登录、商店、IAP或广告。新iOS编译仅PlayerScripts，非签名IPA/真机验证。", 34, y-8, 544, size=8.8)-10
    b.paragraph("保存catalog分析未请求生产端点；六PNG、首页32输入与Packages877保持不变。每阶段绑定实际411清单；归档到最终仅Editor初始化handle及Loading字形校验差异，生产输入与bundle生成方法原字不变。全部本地，无push/publish。证据：Logs/ClientDownloadLoadingReview。", 34, y, 544, size=8.3, color="#65717B")
    b.finish()


def render_and_check(output: Path, directory: Path, dpi: int) -> dict:
    import pymupdf
    from pypdf import PdfReader
    reader = PdfReader(output)
    require(len(reader.pages) == 2, "Written client report has another page count")
    text = [page.extract_text() or "" for page in reader.pages]
    require(all(f"{i} / 2" in page for i, page in enumerate(text, 1)), "Client report page numbering failed")
    require("173" in text[1] and "211" in text[1] and "localhost" in text[1] and "fixture" in text[0]
            and "Yes" in text[1] and "20.2KB" in text[0],
            "Actual cache differences or source/scope captions are missing")
    require(any(ref.get_object().get("/FontDescriptor") and "/FontFile2" in ref.get_object()["/FontDescriptor"].get_object()
                for page in reader.pages for ref in page["/Resources"]["/Font"].get_object().values()), "Chinese TrueType font is not embedded")
    directory.mkdir(parents=True, exist_ok=True)
    rendered = []
    with pymupdf.open(output) as doc:
        for i, page in enumerate(doc, 1):
            for block in page.get_text("dict")["blocks"]:
                if block.get("type") == 0:
                    x0, y0, x1, y1 = block["bbox"]
                    require(min(x0, y0) >= 0 and x1 <= page.rect.width and y1 <= page.rect.height,
                            "Actual PDF text extends beyond page bounds")
            path = directory/f"page-{i:02d}.png"
            page.get_pixmap(matrix=pymupdf.Matrix(dpi/72, dpi/72), alpha=False).save(path)
            rendered.append(str(path))
    return {"pages": 2, "renderedPages": rendered, "visualInspectionRequired": True}


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--root", type=Path, default=ROOT)
    p.add_argument("--output", type=Path, default=ROOT/"output/pdf"/OUTPUT_NAME)
    p.add_argument("--render-dir", type=Path, default=ROOT/"tmp/pdfs/client-download-loading")
    p.add_argument("--font", type=Path, default=FONT_DEFAULT)
    p.add_argument("--jpeg-quality", type=int, default=88)
    p.add_argument("--render-dpi", type=int, default=120)
    p.add_argument("--validate-only", action="store_true")
    p.add_argument("--validate-sources-only", action="store_true")
    args = p.parse_args()
    root, output = args.root.resolve(), args.output.resolve()
    require(output.name == OUTPUT_NAME, "Use the independent client download/loading filename")
    require(70 <= args.jpeg_quality <= 95 and 72 <= args.render_dpi <= 200, "Invalid JPEG/render quality")
    e = verify(root, sources_only=args.validate_sources_only)
    if args.validate_only or args.validate_sources_only:
        print(json.dumps({"validated": True, "sourcesOnly": args.validate_sources_only,
                          "currentNativeCacheLoadingCompileVerified": not args.validate_sources_only,
                          "standaloneChecks": 94, "catalogKeyComparison": e["source"]["catalogComparison"]}, indent=2))
        return 0
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.stem+".building.pdf")
    try:
        build(e, temporary, args.font, args.jpeg_quality)
        require(temporary.stat().st_size < 10_000_000, "Client PDF exceeds 10 MB")
        render = render_and_check(temporary, args.render_dir.resolve(), args.render_dpi)
        temporary.replace(output)
        paths = {**e["source"]["paths"], "nativeLoading": e["loading"]["path"], **e["native"]["paths"]}
        receipt = {"createdUtc": datetime.now(timezone.utc).isoformat(), "output": str(output),
                   "sha256": sha256(output), "bytes": output.stat().st_size, "pages": 2,
                   "reports": {key: {"path": str(path), "sha256": sha256(path)} for key, path in paths.items()},
                   "embeddedFrames": [{"viewport": view, "type": kind, "path": str(e["loading"]["frames"][(view, kind)]),
                                       "sha256": sha256(e["loading"]["frames"][(view, kind)])}
                                      for view in ("375x667", "768x1024") for kind in ("downloadScreenshot", "instructionScreenshot")]
                                      + [{"viewport": "375x667", "type": "actualStartupAutoDownload", "path": str(e["native"]["automaticProgressScreenshot"]),
                                          "sha256": sha256(e["native"]["automaticProgressScreenshot"])}],
                   "jpegQuality": args.jpeg_quality, "wholeNativeFrames": True, "render": render,
                   "nativeLoadingInventory": e["loading"]["inventory"], "nativeRunnerEvidence": e["native"]}
        output.with_suffix(".receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2, default=str)+"\n", encoding="utf-8")
        print(json.dumps({"output": str(output), "sha256": receipt["sha256"], "bytes": receipt["bytes"],
                          "pages": 2, "renderedPages": render["renderedPages"]}, ensure_ascii=False, indent=2))
    finally:
        if temporary.exists():
            temporary.unlink()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, KeyError, ValueError, OSError) as error:
        print(f"Client-download/loading evidence is not ready: {error}", file=sys.stderr)
        raise SystemExit(2)

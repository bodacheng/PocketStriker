#!/usr/bin/env python3
"""Four-page frozen V6 to approved V3 / runtime V7 color-block review.

Run --validate-only before authoring. The parent must invoke the PDF skill
operation marker immediately before the first authoring command. This script
never runs Unity or saves to Library, and never edits previous deliverables.
"""
from __future__ import annotations

import argparse
import difflib
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys

from build_menu_background_review_pdf import (
    ACCENTS, BASE_HEAD, COLORS, EvidenceError, FONT_DEFAULT, LABELS, PdfBuilder,
    VIEWPORTS, case_index, compare_controls, load_json, number, passed_report,
    require, sha256, utc_value,
)
from build_menu_simple_grid_review_pdf import assets_by_color, native_frames, parity_files, verify_capture_inventory

ROOT = Path(__file__).resolve().parents[2]
REVIEW = Path("Logs/MenuColorBlockReview")
OUTPUT_NAME = "PocketStriker-Menu-Color-Blocks-20261003.pdf"
APPROVED = Path("output/artproposals/RedGrid-Preview-ColorBlock-20261003.png")
APPROVED_SHA = "d52cc22932de74214244f71b01dc24bc882853b5a98f8250226f2fa2e0fcf89d"
V6_REPORT_SHA = "86eafcd16460b65443c22485b9d6525b8749c021423fd6ece475b5b06c3ad0bb"
V6_MANIFEST_SHA = "b09787e95e85d075349de0e070e699269ef2504cc0878c2acb8d95e7e64aebd5"
PROTECTED = {
    "PocketStriker-Menu-Backgrounds-20261003.pdf": "ecb78745710f76eaf92d300a4aa8d2faf6062063a4634547a170fa9f59ee34f7",
    "PocketStriker-Menu-Simple-Grid-20261003.pdf": "878bf3279859dd0b1617ba1d9e102bdd4ed23ec9d968d42f1d088c35a9e2748c",
}
IMMUTABLE_FINAL_ARTIFACTS = {
    "After/Runtime/report.json": "27a6a2cf256e8aba4bc0d4fa201cf0b195f2a35dfb00fbc5862cd6412aa07dd3",
    "After/run-receipt.json": "bd8c456427b7af6131b842a93500186d5db7478452ad683ec280b46030a0c3ce",
    "After/source-parity.json": "19ad3b79989bab3a83b55e6188a8237999f5695226f512c48ee41028a7094263",
    "Regression/geometry-report.json": "174511140f642c6105feb4da4c8756a1771ee1c80b3b583a181542c46664c56d",
    "Regression/ui-pointer-report.json": "4dc0107f0d36b813a8f9f2c85a2133930841d33159cf61a619a216f50f137581",
    "Regression/compile-iOS-report.json": "56c32133270d7dda69a97df06edc4acfef51162f7a0aa50578f07e7bd701754a",
    "Regression/run-receipts.json": "adaea42374be58cdb8dd8b5be46db01d4e71ff278a2fd85588e90c270a34ff0a",
}


def finite(value: object, name: str) -> float:
    require(isinstance(value, (int, float)) and not isinstance(value, bool)
            and math.isfinite(value), f"Missing finite measurement: {name}")
    return float(value)


def check_receipt(data: dict, name: str, native_path: Path, parity_path: Path,
                  earliest: datetime, native: dict, *, full_after: bool = False) -> dict:
    runs = data.get("runs", [])
    matching = [run for run in runs if run.get("name") == name]
    require(len(matching) == 1, f"Exactly one actual runner receipt is required: {name}")
    run = matching[0]
    require(run.get("exitCode") == 0 and run.get("sourceBaseHead") == BASE_HEAD,
            f"Runner failed or source base differs: {name}")
    require(run.get("nativeReportSha256") == sha256(native_path)
            and run.get("sourceParityManifestSha256") == sha256(parity_path),
            f"Runner identifies other native/source bytes: {name}")
    start = utc_value(run.get("startedUtc"), name + " start")
    end = utc_value(run.get("finishedUtc"), name + " finish")
    require(earliest <= start <= end, f"Runner predates this final source run: {name}")
    require(isinstance(run.get("command"), list) and bool(run["command"]), f"No actual command: {name}")
    if name in ("geometry", "pointer"):
        require(start <= utc_value(native.get("utcTime"), name + " native UTC") <= end,
                f"New native regression UTC is outside its runner interval: {name}")
        method = {"geometry": "PocketStrikerUIArtValidation.ValidateGeometryBatch",
                  "pointer": "PocketStrikerUIArtValidation.StartBatch"}[name]
        require("-executeMethod" in run["command"] and method in run["command"]
                and "-buildTarget" in run["command"] and "iOS" in run["command"],
                f"New native regression executes another method/target: {name}")
    if name == "compile":
        require(len(run["command"]) == 2 and Path(run["command"][0]).name == "validate_unity.sh"
                and run["command"][1] == "compile", "New compile did not execute the actual player-script validator")
    if full_after:
        stamp = utc_value(native.get("utcTime"), "native After")
        require(start <= stamp <= end, "Native After time is outside its actual runner interval")
        command, env = run["command"], run.get("environment", {})
        require("-executeMethod" in command and "PocketStrikerMenuBackgroundValidation.StartAfterBatch" in command
                and "-buildTarget" in command and "iOS" in command, "Native After is not the full iOS-target Editor suite")
        wanted = {"POCKETSTRIKER_MENU_REVIEW_ROOT": str(REVIEW), "POCKETSTRIKER_MENU_REVIEW": "After",
                  "POCKETSTRIKER_MENU_SOURCE_SET": "Pattern-v7", "POCKETSTRIKER_MENU_SOURCE_HEAD": BASE_HEAD,
                  "POCKETSTRIKER_MENU_UNIT_WIDTH": "260", "POCKETSTRIKER_MENU_UNIT_HEIGHT": "262"}
        require(all(env.get(k) == v for k, v in wanted.items()), "Final native run environment differs")
        require(env.get("POCKETSTRIKER_MENU_DYNAMIC_REVIEW") == "0", "Final full run is not the independent static suite")
    return run


def verify_native_units(root: Path, assets: dict, report: dict, cases: dict) -> dict:
    from PIL import Image
    tiles = report.get("nativeTileChecks", [])
    require(len(tiles) == 6 and [v.get("color") for v in tiles] == list(COLORS), "Native six-color unit checks missing")
    checks = {}
    for i, color in enumerate(COLORS):
        asset, tile = assets[color], tiles[i]
        width, height = asset["unitWidth"], asset["unitHeight"]
        require((width, height) == (260, 262) and (asset["width"], asset["height"]) == (1560, 1572), "Unexpected native repeat geometry")
        with Image.open(root / asset["destination"]) as image:
            require(image.size == (1560, 1572), "Runtime PNG dimensions differ")
            image = image.convert("RGBA")
            unit = image.crop((0, 0, width, height)).tobytes()
            require(all(image.crop((x*width, y*height, (x+1)*width, (y+1)*height)).tobytes() == unit
                        for y in range(6) for x in range(6)), f"Runtime repeat units differ: {color}")
            require(image.crop((0, 0, 1, image.height)).tobytes() == image.crop((image.width-1, 0, image.width, image.height)).tobytes()
                    and image.crop((0, 0, image.width, 1)).tobytes() == image.crop((0, image.height-1, image.width, image.height)).tobytes(),
                    f"Decoded runtime tile edge mismatch: {color}")
        unit_sha = hashlib.sha256(unit).hexdigest()
        require(tile.get("texturePath") == asset["destination"] and tile.get("pngSha256") == asset["runtimeSha256"]
                and tile.get("unitRgbaSha256") == unit_sha, f"Native decoded unit bytes differ: {color}")
        require(tile.get("cellByteFormat") == "RGBA32 (four 8-bit channels), top-left row order"
                and tile.get("unitWidth") == width and tile.get("unitHeight") == height
                and tile.get("width") == 1560 and tile.get("height") == 1572
                and tile.get("rows") == tile.get("columns") == 6 and tile.get("uniqueCells") == 1
                and tile.get("cellByteCount") == width*height*4
                and all(tile.get(k) is True for k in ("horizontalEdgesEqual", "verticalEdgesEqual", "everyCellIdentical")),
                f"Native repeat/unit coverage failed: {color}")
        checks[color] = {"unitWidth": width, "unitHeight": height, "unitsCompared": 36,
                         "unitRgbaSha256": unit_sha, "oppositeEdgesExact": True}
        for view in VIEWPORTS:
            c = cases[(view, color)]
            require(c.get("nativeTileCheckIndex") == i and c.get("unitRgbaSha256") == unit_sha
                    and c.get("unitWidth") == width and c.get("unitHeight") == height
                    and c.get("cellRows") == c.get("cellColumns") == 6, "Native case/unit binding differs")
            vx, vy = 1560*c["uvRect"]["width"]/width, 1572*c["uvRect"]["height"]/height
            px, py = c["backgroundScreenRect"]["width"]/vx, c["backgroundScreenRect"]["height"]/vy
            for key, val in (("visibleCellsX", vx), ("visibleCellsY", vy), ("screenCellPixelsX", px), ("screenCellPixelsY", py)):
                require(abs(finite(c.get(key), key)-val) < .001, "Native unit density does not match actual UV")
            require(abs(px/width-py/height) < .001, "Native source pixels stretch unequally")
    return checks


def verify_scroll(cases: dict, runtime: Path) -> list[dict]:
    result = []
    for (view, color), case in cases.items():
        s = case.get("scroll", {})
        elapsed = finite(s.get("scaledElapsed"), "scaled scroll interval")
        require(elapsed > 0 and number(s, "frames", positive=True) > 0
                and finite(s.get("realtimeElapsed"), "real scroll interval") >= .2, "No actual timed scroll sample")
        error = math.hypot(*(s["actualUvDelta"][k]-s["expectedUvDelta"][k] for k in ("x", "y")))
        require(error < .00001 and abs(error-s["uvDeltaError"]) < .000001, "Actual production scroll differs from frame-integrated speed")
        for axis in ("x", "y"):
            require(abs(s["actualUvDelta"][axis]/elapsed-s["measuredUvPerSecond"][axis]) < .00001, "Timed UV speed does not match actual delta")
            extent = case["backgroundScreenRect"]["width" if axis == "x" else "height"]
            uvsize = case["uvRect"]["width" if axis == "x" else "height"]
            require(abs(-s["measuredUvPerSecond"][axis]*extent/uvsize-s["patternScreenPixelsPerSecond"][axis]) < .01,
                    "Measured pattern screen speed differs from its UV measurement")
            require(abs(s["wrapBeforeUv"][axis]-.99) < .00001 and abs(s["wrapAfterUv"][axis]-1.01) < .00001,
                    "Wrap frames do not straddle the true integer texture boundary")
        for label, key in (("before", "wrapBeforeScreenshot"), ("after", "wrapAfterScreenshot")):
            expected = runtime/view/f"{color}-wrap-{label}.png"
            require(s[key].endswith(f"/{view}/{color}-wrap-{label}.png") and expected.is_file(), "Wrap screenshot source missing")
        result.append({"viewport": view, "color": color, **s})
    require(len(result) == 24, "Timed/native boundary sample coverage incomplete")
    return result


def verify_motion(path: Path, after: dict, parity_path: Path, earliest: datetime, root: Path = ROOT) -> dict:
    """Bind real native long captures, allowing a separately recorded retry label."""
    from PIL import Image
    report = load_json(path)
    directory = path.parent.parent
    require(directory.name.startswith("Preview-red"), "Motion input is not the recorded red capture run")
    source_path, runner_path = directory/"source-parity.json", directory/"run-receipt.json"
    source = load_json(source_path)
    current = parity_files(load_json(parity_path))
    captured = parity_files(source)
    require(set(current) == set(captured) and all(captured[p]["sha256"] == current[p]["sha256"]
            and captured[p].get("sameExecutionBytes") is True for p in current),
            "Long-capture production/fixture bytes differ from final V7")
    passed_report(report, "Actual native red long capture", complete=True)
    require(report.get("dynamicReview") is True and report.get("previewOnly") is True
            and report.get("sourceSet") == "Pattern-v7" and report.get("unityVersion") == after.get("unityVersion")
            and report.get("externalServicesIsolated") is True and report.get("sourceAssetsUnchanged") is True,
            "Motion source is not this completed isolated V7 native capture")
    run = check_receipt(load_json(runner_path), directory.name, path, source_path, earliest, report)
    start, end = utc_value(run["startedUtc"], "capture start"), utc_value(run["finishedUtc"], "capture finish")
    require(start <= utc_value(report["utcTime"], "capture native UTC") <= end, "Motion report time is outside its actual execution")
    env, command = run.get("environment", {}), run["command"]
    require(env.get("POCKETSTRIKER_MENU_REVIEW_ROOT") == str(REVIEW)
            and env.get("POCKETSTRIKER_MENU_REVIEW") == directory.name
            and env.get("POCKETSTRIKER_MENU_SOURCE_SET") == "Pattern-v7"
            and env.get("POCKETSTRIKER_MENU_SOURCE_HEAD") == BASE_HEAD
            and env.get("POCKETSTRIKER_MENU_DYNAMIC_REVIEW") == "1"
            and env.get("POCKETSTRIKER_MENU_UNIT_WIDTH") == "260"
            and env.get("POCKETSTRIKER_MENU_UNIT_HEIGHT") == "262"
            and "-buildTarget" in command and "iOS" in command and "-executeMethod" in command
            and "PocketStrikerMenuBackgroundValidation.StartRedArtPreviewBatch" in command,
            "Long capture receipt did not execute the actual dynamic preview")
    sequences = report.get("dynamicSequences", [])
    require(len(sequences) == 2 and {s.get("viewport") for s in sequences} == {"375x667", "768x1024"},
            "Actual red phone/tablet sequences are incomplete")
    declared = set(report.get("screenshots", []))
    result, hashes = [], {}
    for seq in sequences:
        view = seq["viewport"]
        require(seq.get("color") == "red" and finite(seq.get("realtimeElapsed"), "long interval") >= 7
                and number(seq, "updateFrames", positive=True) > 0, "Native long capture did not run for seven seconds")
        samples = seq.get("samples", [])
        require(len(samples) >= 20 and [s.get("index") for s in samples] == list(range(len(samples))), "Recorded motion frames incomplete/unordered")
        uv_error = math.hypot(*(seq["actualUvDelta"][k]-seq["expectedUvDelta"][k] for k in ("x", "y")))
        require(uv_error < .0001 and abs(uv_error-seq["uvDeltaError"]) < .000001, "Long sequence changes actual authored speed")
        verified, previous_time, previous_frame = [], -1.0, -1
        for sample in samples:
            canonical = path.parent/view/"dynamic-red"/f"red-{sample['index']:03d}.png"
            require(sample["screenshot"] in declared and sample["screenshot"].endswith(f"/{view}/dynamic-red/{canonical.name}"),
                    "Native frame reference not declared in its actual capture report")
            with Image.open(canonical) as image:
                require(image.size == tuple(map(int, view.split("x"))), "Motion screenshot is not its native full backbuffer")
            original = Path(sample["screenshot"])
            require(not original.is_file() or sha256(original) == sha256(canonical), "Actual captured frame bytes differ from canonical copy")
            timestamp = utc_value(sample["utcTime"], "native frame request UTC")
            realtime = finite(sample.get("realtimeElapsed"), "native frame clock")
            require(start <= timestamp <= end and realtime > previous_time
                    and sample["unityFrame"] > previous_frame, "Motion frame request clocks are inconsistent")
            previous_time, previous_frame = realtime, sample["unityFrame"]
            require(math.hypot(*(sample["uvRect"][k]-sample["expectedUvPosition"][k] for k in ("x", "y"))) < .0001,
                    "Captured frame UV differs from real accumulated scaled-frame movement")
            h = sha256(canonical)
            hashes[str(canonical)] = h
            verified.append({**sample, "canonicalScreenshot": str(canonical), "pngSha256": h})
        require(verified[-1]["realtimeElapsed"] >= 7 and verified[0]["pngSha256"] != verified[-1]["pngSha256"],
                "Long sequence has no final seven-second frame or actual rendered movement")
        result.append({**seq, "samples": verified})
    require(number(report, "dynamicFrameChecks") == sum(len(s["samples"]) for s in sequences), "Native frame check count incomplete")
    motion = {"reportPath": str(path), "reportSha256": sha256(path), "sourceParityPath": str(source_path),
            "sourceParitySha256": sha256(source_path), "runnerReceiptPath": str(runner_path),
            "runnerReceiptSha256": sha256(runner_path), "runner": run, "sequences": result,
            "screenshotHashes": hashes, "frameCount": len(hashes),
            "limitation": "Phone/tablet recorded in separate sessions. Frame UTC/UV are capture-request measurements. Native PNG frames remain whole; GIF may align relative time and loop replay is not a tile seam."}
    gif_path = root/"output/artproposals/PocketStriker-Menu-Color-Blocks-Scroll-20261003.gif"
    evidence_path = gif_path.with_suffix(".evidence.json")
    gif = load_json(evidence_path)
    require(gif.get("status") == "passed" and gif.get("reportSha256") == sha256(path)
            and gif.get("sourceSet") == "Pattern-v7" and gif.get("approvedSourceSha256") == APPROVED_SHA,
            "Companion GIF evidence is bound to another capture/source")
    require(gif.get("output") == str(gif_path.relative_to(root)) and gif.get("outputSha256") == sha256(gif_path)
            and gif.get("bytes") == gif_path.stat().st_size < 10_000_000, "Companion GIF bytes do not match actual evidence")
    source_frames = gif.get("sourceFrames", [])
    require(len(source_frames) == len(hashes) and len({row["path"] for row in source_frames}) == len(hashes),
            "Companion GIF source inventory is incomplete or duplicate")
    by_view = {s["viewport"]: s for s in result}
    for row in source_frames:
        sample = by_view[row["viewport"]]["samples"][row["index"]]
        require(str(root/row["path"]) == sample["canonicalScreenshot"] and row["sha256"] == sample["pngSha256"]
                and row["requestUtc"] == sample["utcTime"] and row["requestRelativeRealtime"] == sample["realtimeElapsed"]
                and row["requestUv"] == sample["uvRect"], "GIF mapping differs from native recorded frame")
    selections = gif.get("selections", [])
    require(len(selections) == gif.get("frames"), "GIF relative-time mapping is incomplete")
    for selection in selections:
        time = selection["outputRelativeRealtime"]
        for view, seq in by_view.items():
            chosen = selection["views"][view]
            nearest = min(seq["samples"], key=lambda s: abs(s["realtimeElapsed"]-time))
            require(chosen["index"] == nearest["index"] and chosen["recordedRelativeRealtime"] == nearest["realtimeElapsed"],
                    "GIF relative-time mapping does not select the closest actual frame")
    with Image.open(gif_path) as decoded:
        require(decoded.n_frames == gif["frames"] and list(decoded.size) == gif["outputSize"], "GIF decoded dimensions/frame count differ")
        durations = []
        for i in range(decoded.n_frames):
            decoded.seek(i); decoded.load(); durations.append(decoded.info.get("duration", 0))
    require(durations == gif["durationsMs"] and all(duration > 0 for duration in durations), "GIF actual frame timing differs from evidence")
    motion["gif"] = {"path": str(gif_path), "sha256": sha256(gif_path), "bytes": gif_path.stat().st_size,
                     "evidencePath": str(evidence_path), "evidenceSha256": sha256(evidence_path),
                     "frames": len(durations), "size": gif["outputSize"], "playbackMs": sum(durations),
                     "recordedIntervalPlaybackMs": sum(durations[:-1]), "finalHoldMs": durations[-1],
                     "limitation": gif["limitation"]}
    return motion


def verify_phase_probe(path: Path, parity_path: Path, root: Path) -> dict:
    report = load_json(path)
    directory = path.parent.parent
    final = load_json(parity_path)
    final_files = parity_files(final)
    fixture = "Assets/Editor/PocketStrikerMenuBackgroundValidation.cs"
    source_path, execution_path = directory/"source-parity.json", directory/"execution-parity.json"
    restore_path, runner_path = directory/"restore-parity.json", directory/"run-receipt.json"
    source, restored = load_json(source_path), load_json(restore_path)
    require(sha256(source_path) == sha256(execution_path), "Controlled execution/source manifests differ")
    require(source.get("baseHead") == BASE_HEAD and source.get("sourceSet") == "Pattern-v7"
            and source.get("unchangedProductionAndOtherInputFiles") == 16
            and source.get("controlledEditorFixtureDifferences") == 1, "Native probe instrumentation scope is not sixteen same plus one fixture")
    controlled = {row["path"]: row for row in source.get("files", [])}
    require(len(source.get("files", [])) == len(controlled) == 17 and set(controlled) == set(final_files), "Native probe source inventory differs")
    for name, row in controlled.items():
        expected = final_files[name]["sha256"]
        require(row.get("immutableRootSha256") == expected == sha256(root/name), "Immutable final root bytes changed during probe")
        if name == fixture:
            require(row.get("sameExecutionBytes") is False and row.get("controlledEditorInstrumentation") is True
                    and row.get("executionSha256") != expected, "Missing sole controlled Editor fixture difference")
        else:
            require(row.get("sameExecutionBytes") is True and row.get("controlledEditorInstrumentation") is False
                    and row.get("executionSha256") == expected, "Probe changed a production or other input file")
    immutable, instrumented = directory/"Source/Immutable"/fixture, directory/"Source/Instrumented"/fixture
    diff_path = directory/"Source/minimal.diff"
    require(sha256(immutable) == source["immutableFinalFixtureSha256"] == final_files[fixture]["sha256"], "Immutable fixture backup differs")
    require(sha256(instrumented) == source["instrumentedExecutionFixtureSha256"] == controlled[fixture]["executionSha256"]
            == "d416392d4b770c2724d45abcac2b6276bf0954caed0e91d8f04c06a43da0de31", "Instrumentation is not the reviewed paused-phase fixture")
    diff = "".join(difflib.unified_diff(immutable.read_text().splitlines(keepends=True),instrumented.read_text().splitlines(keepends=True),
                                       fromfile="immutable-root/"+fixture,tofile="instrumented-clone/"+fixture))
    require(diff_path.read_text() == diff, "Saved minimal diff does not match actual controlled source bytes")
    for rel, digest in IMMUTABLE_FINAL_ARTIFACTS.items():
        require(sha256(root/REVIEW/rel) == digest, "Original final run/regression report or receipt was changed by companion probe")
    passed_report(report, "Independent native phase", complete=True)
    require(report.get("temporaryNativePhaseInstrumentation") is True and report.get("artRefresh") is True
            and report.get("previewOnly") is False and report.get("dynamicReview") is False
            and report.get("sourceSet") == "Pattern-v7" and report.get("sourceHead") in (BASE_HEAD,BASE_HEAD[:9])
            and report.get("externalServicesIsolated") is True and report.get("sourceAssetsUnchanged") is True,
            "Native phase report is not the isolated controlled companion")
    require(number(report,"nativeReturnPhaseChecks") == 24 and number(report,"nativeHomeReloadPhaseChecks") == 24
            and number(report,"nativeNavigationPhaseChecks") == 48, "Native phase navigation coverage incomplete")
    require("all six scrollers paused" in report.get("nativePhaseScope", "")
            and "before any fixture CenterUV" in report["nativePhaseScope"], "Native phase scope does not disclose paused/pre-normalization assertions")
    cases = case_index(report, "Independent phase")
    authored = report.get("authoredUvCenters", [])
    require(len(authored) == 6, "Native authored center inventory incomplete")
    max_error, minimum_nondefault, comparisons = 0.0, math.inf, 0
    for (view, color), case in cases.items():
        require(case.get("nativeReturnPhasePreserved") is True and case.get("nativeHomeReloadPhasePreserved") is True,
                "A native Return/HOME phase assertion failed")
        for operation, xbase, ybase in (("nativeReturn",.713,.819),("nativeHomeReload",.831,.927)):
            before, after = case[operation+"RawUvBefore"], case[operation+"RawUvAfter"]
            require(len(before) == len(after) == 6, "Probe omitted active/inactive background UV captures")
            for index, (a,b) in enumerate(zip(before,after)):
                error = max(math.hypot(a["x"]-b["x"],a["y"]-b["y"]),
                            math.hypot(a["width"]-b["width"],a["height"]-b["height"]))
                require(error < .00001, "Actual raw Return/HOME rectangles reset before normalization")
                center = (a["x"]+a["width"]*.5,a["y"]+a["height"]*.5)
                expected = (xbase+index*.011+COLORS.index(color)*.001,ybase-index*.009+int(view.split("x")[1])*.000001)
                require(math.hypot(center[0]-expected[0],center[1]-expected[1]) < .000001, "Captured nondefault UV does not match reviewed injection")
                nondefault = math.hypot(center[0]-authored[index]["x"],center[1]-authored[index]["y"])
                require(nondefault > .1, "Phase probe accidentally tested the authored default center")
                max_error, minimum_nondefault = max(max_error,error), min(minimum_nondefault,nondefault)
                comparisons += 1
    regression_runs = load_json(root/REVIEW/"Regression/run-receipts.json")["runs"]
    earliest = max(utc_value(run["finishedUtc"], "fresh regression finish") for run in regression_runs)
    run = check_receipt(load_json(runner_path), directory.name, path, source_path, earliest, report)
    require(utc_value(run["startedUtc"], "phase start") <= utc_value(report["utcTime"], "phase native UTC")
            <= utc_value(run["finishedUtc"], "phase finish"), "Native phase report UTC outside actual run")
    command, env = run["command"], run.get("environment", {})
    require("PocketStrikerMenuBackgroundValidation.StartArtRefreshBatch" in command and "-buildTarget" in command
            and "iOS" in command and env.get("POCKETSTRIKER_MENU_REVIEW") == directory.name
            and env.get("POCKETSTRIKER_MENU_SOURCE_SET") == "Pattern-v7"
            and env.get("POCKETSTRIKER_MENU_DYNAMIC_REVIEW") == "0", "Native phase did not execute its controlled companion mode")
    # Independently read restored filesystem bytes as well as the saved receipt.
    restored_rows = {row["path"]: row for row in restored.get("files", [])}
    require(restored.get("rootSource17Unchanged") is True and restored.get("restored17InputsEqual") is True
            and restored.get("controlledEditorDifferenceDuringRun") == 1 and restored.get("sameOther16InputsDuringRun") is True
            and restored.get("baseHead") == BASE_HEAD and restored.get("sourceSet") == "Pattern-v7",
            "Final restoration does not certify the controlled sixteen-plus-one scope")
    require(len(restored_rows) == 17 and set(restored_rows) == set(final_files), "Restoration report does not cover all seventeen inputs")
    for name, row in restored_rows.items():
        expected = final_files[name]["sha256"]
        require(sha256(root/name) == sha256(Path(final["executionProject"])/name) == expected,
                "Root or execution source was not restored to final bytes")
        root_hash = row.get("rootSha256",row.get("immutableRootSha256",row.get("sha256")))
        execution_hash = row.get("restoredExecutionSha256",row.get("executionSha256", row.get("sha256") if row.get("sameExecutionBytes") is True else None))
        require(root_hash == execution_hash == expected, "Restoration manifest identifies other bytes")
    restore_time = restored.get("utcTime",restored.get("restoredUtc"))
    require(utc_value(restore_time,"restoration UTC") >= utc_value(run["finishedUtc"],"phase finish"), "Restoration evidence predates probe completion")
    bindings = {"native_phase":path,"phase_run":runner_path,"phase_execution":execution_path,"phase_parity":source_path,
                "phase_restore":restore_path,"phase_diff":diff_path,"phase_immutable_fixture":immutable,"phase_instrumented_fixture":instrumented}
    return {"returnChecks":24,"homeReloadChecks":24,"navigationChecks":48,"backgroundRectComparisons":comparisons,
            "maximumRectError":max_error,"minimumNondefaultCenterDistance":minimum_nondefault,
            "scrollersPaused":True,"assertionsBeforeCenterUV":True,"controlledFixtureDifferences":1,
            "unchangedOtherInputs":16,"restoredInputs":17,"runner":run,"scope":report["nativePhaseScope"],
            "bindings":{name:{"path":str(p),"sha256":sha256(p)} for name,p in bindings.items()}}


def verify(root: Path, motion_path: Path, *, native_only: bool = False, phase_path: Path | None = None) -> dict:
    review, frozen = root/REVIEW, root/REVIEW/"Before-v6"
    paths = {"before": frozen/"Runtime/report.json", "manifest": frozen/"Source/manifest.json",
             "before_parity": frozen/"source-parity.json", "after": review/"After/Runtime/report.json",
             "build": review/"build.json", "readback": review/"build-readback-audit.json",
             "configuration": review/"scene-configuration.json", "parity": review/"After/source-parity.json",
             "after_run": review/"After/run-receipt.json", "regression_runs": review/"Regression/run-receipts.json",
             "geometry": review/"Regression/geometry-report.json", "pointer": review/"Regression/ui-pointer-report.json",
             "compile": review/"Regression/compile-iOS-report.json", "motion": motion_path}
    pending_regressions = {"regression_runs", "geometry", "pointer", "compile"} if native_only else set()
    reports = {k: load_json(p) for k, p in paths.items() if k != "motion" and k not in pending_regressions}
    require(sha256(root/APPROVED) == APPROVED_SHA, "Approved Library-v3 local red source changed")
    require(sha256(paths["before"]) == V6_REPORT_SHA and sha256(paths["manifest"]) == V6_MANIFEST_SHA, "Before is not frozen preceding final V6")
    manifest = reports["manifest"]
    require(manifest.get("sourceSet") == "Pattern-v6" and manifest.get("originalEvidenceUnchanged") is True
            and manifest.get("runtimePngCount") == 96, "V6 frozen manifest is incomplete")
    require(subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, check=True, text=True, capture_output=True).stdout.strip() == BASE_HEAD,
            "Source base HEAD changed")
    backups = {entry["path"]: entry for entry in manifest["files"]}
    require(len(backups) == 17, "V6 source backup inventory differs")
    for p, row in backups.items():
        require(sha256(frozen/"Source"/p) == row["sha256"], "Frozen V6 source backup changed")
    for name in ("before", "after"):
        passed_report(reports[name], name, complete=True)
        require(reports[name].get("baseline") is False and reports[name].get("previewOnly") is False
                and reports[name].get("externalServicesIsolated") is True and reports[name].get("sourceAssetsUnchanged") is True,
                f"{name}: native report is not the actual isolated completed full run")
        require(reports[name].get("sourceHead") in (BASE_HEAD, BASE_HEAD[:9]), "Native source HEAD differs")
    after = reports["after"]
    require(after.get("sourceSet") == "Pattern-v7" and after.get("artRefresh") is False
            and after.get("dynamicReview") is False, "Final V7 callback suite is incomplete or replaced by preview")
    expected = {"pointerClicks": 80, "navigationCycles": 24, "homeReloads": 24, "nativeTabCycles": 4,
                "viewportCases": 4, "semanticChecks": 15, "scrollingChecks": 24, "scrollSpeedChecks": 24,
                "resizeChecks": 30, "zeroRectRecoveryChecks": 6, "lateTextureRecoveryChecks": 6,
                "legacyReferenceChecks": 6, "invalidReferenceChecks": 24, "invalidReferenceRecoveryChecks": 6,
                "nativeEdgeChecks": 12, "identicalCellChecks": 6, "densityChecks": 24, "approvedScaleChecks": 24,
                "aspectChecks": 126, "preservedPhaseChecks": 134}
    for key, value in expected.items():
        require(number(after, key) == value, f"Full final native coverage incomplete: {key}")
    before_cases, after_cases = case_index(reports["before"], "Frozen V6"), case_index(after, "Final V7")
    comparison = compare_controls(before_cases, after_cases)
    files = parity_files(reports["parity"])
    require(reports["parity"].get("sourceSet") == "Pattern-v7", "Final source parity has another source set")
    for p, row in files.items():
        require(row.get("sameExecutionBytes") is True and sha256(root/p) == row["sha256"], f"Root source parity is stale: {p}")
        copied = Path(reports["parity"]["executionProject"])/p
        require(copied.is_file() and sha256(copied) == row["sha256"], f"Execution source parity is stale: {p}")
    assets = assets_by_color(reports["build"])
    require(reports["build"].get("sourceSet") == "Pattern-v7", "Wrong final source art set")
    passed_report(reports["readback"], "Current six-PNG readback", complete=True)
    require(reports["readback"].get("buildReportSha256") == sha256(paths["build"])
            and reports["readback"].get("approvedRedSha256") == APPROVED_SHA, "Readback is not bound to this approved build")
    require(assets["red"]["sourceSha256"] == APPROVED_SHA, "Runtime red derives from a different unapproved source")
    for color, asset in assets.items():
        for pkey, hkey in (("source", "sourceSha256"), ("destination", "runtimeSha256")):
            require(sha256(root/asset[pkey]) == asset[hkey], "Current runtime/source SHA differs from build")
        meta = asset["destination"]+".meta"
        require(asset.get("metadataUnchanged") is True and sha256(root/meta) == backups[meta]["sha256"], "Existing importer metadata changed")
        require(asset.get("resampling") == "none-native-pixels" and asset.get("nativeInteriorUnchanged") is True
                and asset.get("edgeMatchBand") == 12 and asset.get("strictUniformUnits") is True,
                "Actual build does not support native-crop/repeat technical description")
        for view in VIEWPORTS:
            a, b = before_cases[(view, color)], after_cases[(view, color)]
            require(a["texturePath"] == b["texturePath"] == asset["destination"] and a["textureGuid"] == b["textureGuid"], "Element asset/GUID binding changed")
            require(a["textureSha256"] == backups[asset["destination"]]["sha256"] and b["textureSha256"] == asset["runtimeSha256"], "Native renderer used another texture")
    configuration = reports["configuration"]
    require(configuration.get("rawImageTintWhiteAllSix") is True and configuration.get("allOtherYamlDocumentsByteIdentical") is True
            and configuration.get("sceneSha256") == sha256(root/"Assets/Scene/MainScene/MainMenuScene.unity"), "Final authored scene configuration differs")
    for c in after_cases.values():
        for axis in ("x", "y"):
            require(abs(c["referenceUvSize"][axis]-configuration["referenceUvSize"][axis]) < .00001, "Native reference crop differs from authored scene")
        span = (1560*c["referenceUvSize"]["x"], 1572*c["referenceUvSize"]["y"])
        require(math.hypot(span[0]-941, span[1]-1672) < .001, "Reference source window differs from independently approved portrait")
        target = c["backgroundScreenRect"]["width"]/c["backgroundScreenRect"]["height"]
        approved = (941, 941/target) if target > 941/1672 else (1672*target, 1672)
        sampled = (1560*c["uvRect"]["width"], 1572*c["uvRect"]["height"])
        require(math.hypot(sampled[0]-approved[0],sampled[1]-approved[1]) < .002,
                "Actual viewport sampled density differs from independent approved portrait")
    grid = verify_native_units(root, assets, after, after_cases)
    for stage, directory, report in (("Before-v6", frozen/"Runtime", reports["before"]), ("After", review/"After/Runtime", after)):
        verify_capture_inventory(directory, report)
    frames = native_frames(root, frozen/"Runtime", reports["before"], "Before-v6")
    frames.update(native_frames(root, review/"After/Runtime", after, "After"))
    previous = load_json(root/"output/pdf/PocketStriker-Menu-Simple-Grid-20261003.receipt.json")
    for (stage, view, color), frame in frames.items():
        if stage == "Before-v6":
            require(previous["screenshotHashes"][f"After/{view}/{color}"] == sha256(frame), "Before screenshot is not preceding V6 deliverable source")
    runs = {"After": check_receipt(reports["after_run"], "After", paths["after"], paths["parity"],
                                     utc_value(manifest["savedUtc"], "V6 freeze"), after, full_after=True)}
    for name in (() if native_only else ("geometry", "pointer", "compile")):
        passed_report(reports[name], "New V7 "+name)
        require(reports[name].get("unityVersion") == after.get("unityVersion"), "New regression uses another Unity version")
        runs[name] = check_receipt(reports["regression_runs"], name, paths[name], paths["parity"],
                                  utc_value(runs["After"]["finishedUtc"], "Final After finish"), reports[name])
    if not native_only:
        require(reports["compile"].get("check") == "compile" and reports["compile"].get("activeBuildTarget") == "iOS"
                and reports["compile"].get("expectedUnityVersion") == "6000.5.1f1", "New compile is not the expected iOS player-script check")
    scrolls = verify_scroll(after_cases, review/"After/Runtime")
    motion = verify_motion(motion_path, after, paths["parity"], utc_value(manifest["savedUtc"], "V6 freeze"), root)
    paths.update({"motion_parity": Path(motion["sourceParityPath"]), "motion_run": Path(motion["runnerReceiptPath"]),
                  "motion_gif_evidence": Path(motion["gif"]["evidencePath"])})
    phase = None
    if not native_only:
        phase_path = phase_path or review/"Native-phase/Runtime/report.json"
        phase = verify_phase_probe(phase_path, paths["parity"], root)
        paths.update({name:Path(binding["path"]) for name,binding in phase["bindings"].items()})
    return {"root": root, "review": review, "paths": paths, "reports": reports, "before": before_cases,
            "after": after_cases, "frames": frames, "comparison": comparison, "grid": grid,
            "runs": runs, "scrolls": scrolls, "motion": motion, "phase": phase}


class Builder(PdfBuilder):
    def page(self, title: str, subtitle: str = "") -> None:
        from reportlab.lib.colors import HexColor
        if self.page_no:
            self.canvas.showPage()
        self.page_no += 1
        self.canvas.setFillColor(HexColor("#FAF8F3"))
        self.canvas.rect(0, 0, self.WIDTH, self.HEIGHT, fill=1, stroke=0)
        self.text("POCKETSTRIKER / COLOR BLOCK REVIEW", 34, 770, size=8, color="#647383")
        self.text(title, 34, 746, size=20)
        if subtitle:
            self.paragraph(subtitle, 34, 729, 544, size=9.5, bottom=686)
        self.canvas.setStrokeColor(HexColor("#D8D4CB"))
        self.canvas.line(34, 46, 578, 46)
        self.text("完整 Unity Editor 实景 | 本地验证，非真机认证", 34, 29, size=8, color="#6F7780")
        self.text(f"{self.page_no} / 4", 552, 29, size=8, color="#6F7780")

    def finish(self) -> None:
        require(self.page_no == 4, "Color-block review must have four pages")
        self.canvas.save()


def build(e: dict, output: Path, font: Path, quality: int) -> None:
    b = Builder(output, font, quality)
    b.canvas.setTitle("PocketStriker 获批色块背景实际验收")
    b.page("改后 V7 / 六色实际小屏", "应用获批 v3 小色块方向：连通菱形格内只保留简单填色方块；五元素与 Null 使用同一结构。")
    b.text("375x667 | 原生完整截图 | 此轮真实六状态", 34, 683, size=9, color="#65717B")
    for n, color in enumerate(COLORS):
        x, y = 46+(n%3)*181, 379 if n<3 else 78
        b.text(LABELS[color][0], x, y+282, size=11, color=ACCENTS[color])
        b.image(e["frames"][("After", "375x667", color)], x, y, 155, 155*667/375)
    b.text("Null 是真实无焦点回退；保留红色角色预览。源图仍有轻微色调变化。", 34, 61, size=8.5, color="#65717B")
    b.page("红色 / V6 到色块版", "改前为冻结的最终 V6；改后为本轮 V7。同角色与 idle=0，原生镜头差异见末页。")
    for stage, x in (("Before-v6",112),("After",334)):
        b.text(f"{'改前 V6' if stage=='Before-v6' else '改后 V7'} | 375x667 手机", x-7,700,size=10)
        b.image(e["frames"][(stage,"375x667","red")], x,403,161,161*667/375)
    for stage,x in (("Before-v6",43),("After",327)):
        b.text(f"{'改前 V6' if stage=='Before-v6' else '改后 V7'} | 768x1024 平板",x,390,size=10)
        b.image(e["frames"][(stage,"768x1024","red")],x,62,242,242*1024/768)
    b.page("实际滚动 / 整数边界", "双轴真实 UV 从 (0.99,0.99) 到 (1.01,1.01)，跨纹理边界 1；两帧差值为 (0.02,0.02)。")
    b.text("边界静态采样 / 完整原生截图",34,679,size=10)
    columns=(("375x667", "手机", (34,153)),("768x1024", "平板", (272,425)))
    for view,label,xs in columns:
        width=185*int(view.split("x")[0])/int(view.split("x")[1])
        for suffix,x in zip(("before","after"),xs):
            b.text(f"{label} UV {'0.99' if suffix=='before' else '1.01'}",x,658,size=8.5)
            b.image(e["review"]/"After/Runtime"/view/f"red-wrap-{suffix}.png",x,460,width,185)
    b.text("真正动态采样 / 下列为各独立会话首帧与末帧",34,420,size=10)
    for view,label,xs in columns:
        seq=next(s for s in e["motion"]["sequences"] if s["viewport"]==view)
        width=185*int(view.split("x")[0])/int(view.split("x")[1])
        for sample,x in zip((seq["samples"][0],seq["samples"][-1]),xs):
            b.text(f"{label} t={sample['realtimeElapsed']:.2f}s",x,400,size=8.5)
            b.image(Path(sample["canonicalScreenshot"]),x,204,width,185)
        s=next(v for v in e["scrolls"] if v["viewport"]==view and v["color"]=="red")
        speed=s["patternScreenPixelsPerSecond"]
        x=34 if label=="手机" else 310
        b.text(f"{label}: {len(seq['samples'])}帧/{seq['realtimeElapsed']:.3f}s",x,181,size=8.5)
        b.text(f"图案速度 ({speed['x']:.2f}, {speed['y']:.2f}) px/游戏秒",x,168,size=8.5)
    b.paragraph("边界截图是静态相位采样；速度按逐帧 Time.deltaTime 与实际 UV 位移核对。动态帧为原生 ScreenCapture，在请求时记录 UV/UTC/frame clocks。两种比例分开录制，未声称同时采集。",34,143,544,size=9,bottom=60)
    b.paragraph("完整真实帧、时间及 SHA 保留于执行收据；关联滚动 GIF 仅按相对时间对齐，循环重播的回跳不代表纹理接缝。",34,91,544,size=8.5,bottom=47)
    b.page("本轮结果与实际限制", "所有新回归均绑定最终 V7 源码清单与真实执行时间；改前冻结记录不伪装成本轮重跑。")
    r=e["reports"]["after"]
    rows=[("本轮场景、导航",f"24 场景 / {r['viewportCases']} 比例 / 96 截图；{r['pointerClicks']} 点击；{r['navigationCycles']} 返回；{r['homeReloads']} HOME 重建"),
          ("映射与真实滚动",f"{r['semanticChecks']} 语义；{r['scrollingChecks']} 边界采样；{r['scrollSpeedChecks']} 实测速度；{r['nativeTabCycles']} 页签循环"),
          ("比例、相位与恢复",f"{r['aspectChecks']} 比例；{r['preservedPhaseChecks']} 相位；{r['resizeChecks']} 尺寸切换；零矩形/延迟纹理各 {r['zeroRectRecoveryChecks']} 项"),
          ("新增 reference 回归",f"{r['legacyReferenceChecks']} 旧默认值；{r['invalidReferenceChecks']} 无效值；{r['invalidReferenceRecoveryChecks']} 恢复"),
          ("新增原生相位探针",f"{e['phase']['returnChecks']} 次 Return / {e['phase']['homeReloadChecks']} 次 HOME；非默认 UV 在真实操作前后保留，检查后才归中"),
          ("原生 PNG 重复单位",f"6 张1560x1572；260x262原生单位；每色36单位严格重复；{r['nativeEdgeChecks']}边界 / {r['densityChecks']}密度"),
          ("新几何与原生交互",f"{e['reports']['geometry']['layoutCases']}布局 / {e['reports']['geometry']['compositions']}组合 / {e['reports']['geometry']['stageCardCases']}卡片；{e['reports']['pointer']['settingsTabCases']}设置页签 / {e['reports']['pointer']['raycastChecks']}raycast"),
          ("新 iOS 脚本编译","6000.5.1f1 / iOS通过；三项新回归的独立执行收据均匹配本轮 source-parity.json")]
    y=b.table(rows,699,size=9)-16
    b.text("本轮生产改动",34,y,size=11)
    cfg=e["reports"]["configuration"];ref=cfg["referenceUvSize"]
    y=b.paragraph(f"保留六资源 GUID 与 .meta、布局及切换语义；新增 reference UV ({ref['x']:.6f}, {ref['y']:.6f}) 保持获批原图取景密度，保留 authored UV 中心。六背景 tint 统一白色，包含原 Red/Null 的白化；组件增加 reference-window 与无效值回退。原生周期裁切、12px 边界匹配及重复拷贝，无重采样；单位内有两个小色块，36 个重复单位不等于完整可见的菱形数量。",34,y-8,544,size=9)-14
    b.text("真实执行时间 (UTC)",34,y,size=11)
    y=b.paragraph("\n".join(f"{name}: {e['runs'][name]['startedUtc']} → {e['runs'][name]['finishedUtc']}" for name in ("After","geometry","pointer","compile")),34,y-8,544,size=8)-14
    y=b.paragraph(f"Native phase: {e['phase']['runner']['startedUtc']} → {e['phase']['runner']['finishedUtc']}",34,y,544,size=8)-10
    y=b.paragraph("独立原生相位探针暂停 scrollers，以确定性验证 Return/HOME 的非默认 UV 保留；该探针不证明运动速度。原动态速度仍由 24 项原生计时检查与两个 7 秒真实序列证明。探针仅临时修改执行副本的 Editor fixture；其他 16 输入匹配，结束后 17 输入恢复，原最终报告与三新回归未改。",34,y,544,size=8.4)-12
    b.text("比较控制与验证边界",34,y,size=11)
    c=e["comparison"]["summary"]
    y=b.paragraph(f"24 对照同角色、idle=0 与模型根；原生 bounds-fit 镜头尺寸最大相对差 {c['cameraSizeRelativeDelta']*100:.3f}%，位置最大分量差 {c['cameraPositionDelta']:.6f}。PNG 解码层重复/边界相同，不代表 GPU 压缩或过滤后逐像素相同。获批源仍有轻微色调变化，不声称完全无纹理、严格三 RGB 或单菱形全部像素相同。真实 PreScene.SetFocusingUnit(null) 保留红预览。Unity Editor / 本地 Addressables 离线，隔离账户、广告、IAP；未验证真机触摸、安全区、GPU、线上加载或 IPA/TestFlight。仅本地，无 push/publish。",34,y-8,544,size=8.6)-10
    if e["reports"]["geometry"].get("sceneDirtyOnlyFinding") is True:
        y=b.paragraph(f"原 source-layout 报告仍为 false，含 {e['reports']['geometry']['sourceLayoutErrors']} 项隔离空场景 dirty 标记错误；本报告仅独立几何通过，对象/组件指纹与预览场景数量证实恢复。",34,y,544,size=8.4)-10
    b.paragraph(f"源码基线 {BASE_HEAD}；正式验证及探针恢复后 17 文件哈希一致。证据：Logs/MenuColorBlockReview/Before-v6、After/Runtime、Regression、Native-phase 及独立运行收据。",34,y,544,size=8,color="#65717B")
    b.finish()


def render_and_check(output: Path, directory: Path, dpi: int) -> dict:
    import pymupdf
    from pypdf import PdfReader
    reader=PdfReader(output)
    require(len(reader.pages)==4,"Written PDF has wrong page count")
    texts=[p.extract_text() or "" for p in reader.pages]
    require(all(f"{n} / 4" in text for n,text in enumerate(texts,1)),"PDF page numbering failed")
    require("PreScene.SetFocusingUnit(null)" in texts[3] and "0.02" in texts[2],"Actual method/wrap limitation disclosure missing")
    require(any(ref.get_object().get("/FontDescriptor") and "/FontFile2" in ref.get_object()["/FontDescriptor"].get_object()
                for page in reader.pages for ref in page["/Resources"]["/Font"].get_object().values()),"Chinese TrueType font not embedded")
    directory.mkdir(parents=True,exist_ok=True)
    renders=[]
    with pymupdf.open(output) as doc:
        for n,page in enumerate(doc,1):
            path=directory/f"page-{n:02d}.png"
            page.get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72),alpha=False).save(path)
            renders.append(str(path))
    return {"pages":4,"embeddedChineseTrueType":True,"renderedPages":renders,"visualInspectionRequired":True}


def main() -> int:
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--root",type=Path,default=ROOT)
    p.add_argument("--output",type=Path,default=ROOT/"output/pdf"/OUTPUT_NAME)
    p.add_argument("--render-dir",type=Path,default=ROOT/"tmp/pdfs/menu-colorblock")
    p.add_argument("--motion-report",type=Path,default=ROOT/REVIEW/"Preview-red/Runtime/report.json")
    p.add_argument("--phase-report",type=Path,default=ROOT/REVIEW/"Native-phase/Runtime/report.json")
    p.add_argument("--font",type=Path,default=FONT_DEFAULT)
    p.add_argument("--jpeg-quality",type=int,default=88)
    p.add_argument("--render-dpi",type=int,default=120)
    p.add_argument("--validate-only",action="store_true")
    p.add_argument("--validate-native-only",action="store_true",help="Read-only final native/motion checks; never permits authoring or claims new regressions")
    args=p.parse_args();root=args.root.resolve();output=args.output.resolve()
    require(output.name==OUTPUT_NAME,"Use the new independent color-block filename")
    require(70<=args.jpeg_quality<=95 and 72<=args.render_dpi<=200,"Invalid quality/render settings")
    protected={str(root/"output/pdf"/name):sha256(root/"output/pdf"/name) for name in PROTECTED}
    require(all(protected[str(root/"output/pdf"/name)]==digest for name,digest in PROTECTED.items()),"Existing reviewed PDF bytes changed")
    evidence=verify(root,args.motion_report.resolve(),native_only=args.validate_native_only,phase_path=args.phase_report.resolve())
    if args.validate_only or args.validate_native_only:
        print(json.dumps({"validated":True,"nativeOnly":args.validate_native_only,"newRegressionsVerified":not args.validate_native_only,
                          "independentNativePhaseVerified":evidence["phase"] is not None,
                          "sourceSet":"Pattern-v7","cases":24,"screenshots":96,"dynamicFrames":evidence["motion"]["frameCount"],
                          "verifiedRuns":list(evidence["runs"]),"comparison":evidence["comparison"]["summary"]},ensure_ascii=False,indent=2));return 0
    output.parent.mkdir(parents=True,exist_ok=True)
    temporary=output.with_name(output.stem+".building.pdf")
    try:
        build(evidence,temporary,args.font,args.jpeg_quality)
        require(temporary.stat().st_size<10_000_000,"PDF exceeds the 10MB limit")
        checked=render_and_check(temporary,args.render_dir.resolve(),args.render_dpi)
        require(all(sha256(Path(path))==digest for path,digest in protected.items()),"Prior deliverable changed during authoring")
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    receipt={"path":str(output),"bytes":output.stat().st_size,"sha256":sha256(output),"utc":datetime.now(timezone.utc).isoformat(),
             "baseHead":BASE_HEAD,"beforeSourceSet":"Pattern-v6","afterSourceSet":"Pattern-v7",
             "approvedStaticRedSha256":APPROVED_SHA,"previousPdfHashes":protected,
             "reports":{name:{"path":str(path),"sha256":sha256(path)} for name,path in evidence["paths"].items()},
             "screenshotHashes":{"/".join(key):sha256(path) for key,path in evidence["frames"].items()},
             "comparison":evidence["comparison"],"nativeGridChecks":evidence["grid"],"nativeScrollSamples":evidence["scrolls"],
             "motion":evidence["motion"],"newRegressionReceipts":evidence["runs"],"jpegQuality":args.jpeg_quality,"librarySaved":False,**checked}
    receipt["independentNativePhaseProbe"] = evidence["phase"]
    output.with_suffix(".receipt.json").write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({key:receipt[key] for key in ("path","bytes","sha256","pages","visualInspectionRequired")},ensure_ascii=False,indent=2))
    return 0


if __name__=="__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError,OSError,ValueError,KeyError,subprocess.CalledProcessError) as error:
        print(f"Color-block review stopped: {error}",file=sys.stderr)
        raise SystemExit(2)

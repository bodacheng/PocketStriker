#!/usr/bin/env python3
"""Three-page actual static-gradient review; no Unity or Library operations.

Read-only --validate-only requires fresh native, geometry, pointer and iOS
script-compilation evidence. --validate-native-only checks just the completed
native run and source material; it never permits authoring. Immediately before
the first actual authoring command, the caller must run the PDF skill's artifact
operation marker successfully exactly once. Inspect every latest rendered page
and obtain independent QA before delivery. Previous PDFs are never overwritten.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import sys

from build_menu_background_review_pdf import (
    ACCENTS, BASE_HEAD, COLORS, EvidenceError, FONT_DEFAULT, LABELS, PdfBuilder,
    VIEWPORTS, load_json, number, passed_report, require, sha256, utc_value,
)

ROOT = Path(__file__).resolve().parents[2]
REVIEW = Path("Logs/MenuStaticPixelGradientReview")
SOURCE_SET = "StaticPixelGradient-v1"
OUTPUT_NAME = "PocketStriker-Menu-Static-Pixel-Gradient-20261003.pdf"
APPROVED_SHA = "d69af0e4378b3b61abd9eb3b1481faa9ce2135c619cae7e42218bf2b3e7b753f"
PROTECTED = {
    "PocketStriker-Menu-Backgrounds-20261003.pdf": "ecb78745710f76eaf92d300a4aa8d2faf6062063a4634547a170fa9f59ee34f7",
    "PocketStriker-Menu-Simple-Grid-20261003.pdf": "878bf3279859dd0b1617ba1d9e102bdd4ed23ec9d968d42f1d088c35a9e2748c",
    "PocketStriker-Menu-Color-Blocks-20261003.pdf": "ce45b4cdf8e8ddaa3aebdb3b229286c1ab800bfb2f3953905b12dd5fc9fbcc0c",
}
COUNTS = {
    "viewportCases": 4, "pointerClicks": 80, "navigationCycles": 24,
    "homeReloads": 24, "staticWaitChecks": 24, "modelAnimationChecks": 24,
    "staticNavigationChecks": 48, "actualSceneReloads": 4,
    "staticSceneReloadChecks": 24, "staticGpuPaletteChecks": 6,
    "resizeChecks": 30, "zeroRectRecoveryChecks": 6, "lateTextureRecoveryChecks": 6,
    "staticBadUvRecoveryChecks": 18, "staticScrollerRestartGuardChecks": 6,
    "legacyDefaultModeChecks": 3, "aspectChecks": 24,
}


def finite(value: object, label: str) -> float:
    require(isinstance(value, (int, float)) and not isinstance(value, bool)
            and math.isfinite(value), f"Missing finite measurement: {label}")
    return float(value)


def bound_file(root: Path, item: dict) -> Path:
    path = root / item["path"]
    require(path.is_file() and sha256(path) == item["sha256"], f"Source bytes differ: {path}")
    if "bytes" in item:
        require(path.stat().st_size == item["bytes"], f"Source byte count differs: {path}")
    return path


def expected_uv(width: float, height: float) -> dict:
    u = width / height * 1670 / 942
    return {"x": (1-u)/2, "y": 0.0, "width": u, "height": 1.0}


def check_uv(rect: dict, expected: dict, label: str) -> float:
    delta = max(abs(finite(rect.get(k), f"{label}/{k}")-expected[k])
                for k in ("x", "y", "width", "height"))
    require(delta <= 0.00001, f"Static full-height UV differs: {label}")
    return delta


def check_pair(pair: dict, expected: dict, label: str, start: datetime, end: datetime) -> float:
    require(start <= utc_value(pair.get("startedUtc"), label+" start")
            <= utc_value(pair.get("finishedUtc"), label+" finish") <= end,
            f"Static observation is outside its real execution interval: {label}")
    old, new = pair.get("beforeUv"), pair.get("afterUv")
    require(isinstance(old, list) and isinstance(new, list) and len(old) == len(new) == 6,
            f"Six actual raw UVs are required before and after: {label}")
    delta = 0.0
    for i, (a, b) in enumerate(zip(old, new)):
        check_uv(a, expected, label+f"/{i}/before")
        check_uv(b, expected, label+f"/{i}/after")
        delta = max(delta, *(abs(finite(a[k], label)-finite(b[k], label))
                              for k in ("x", "y", "width", "height")))
    require(delta <= 0.00001, f"Static UV changed across actual operation: {label}")
    return delta


def check_receipt(data: dict, name: str, native_path: Path, parity_path: Path,
                  earliest: datetime, native: dict) -> dict:
    matches = [r for r in data.get("runs", []) if r.get("name") == name]
    require(len(matches) == 1, f"Exactly one actual runner receipt is required: {name}")
    run = matches[0]
    require(run.get("exitCode") == 0 and run.get("sourceBaseHead") == BASE_HEAD,
            f"Runner failed or has another source base: {name}")
    require(run.get("nativeReportSha256") == sha256(native_path)
            and run.get("sourceParityManifestSha256") == sha256(parity_path),
            f"Runner binds another report/source manifest: {name}")
    start, end = utc_value(run.get("startedUtc"), name+" start"), utc_value(run.get("finishedUtc"), name+" finish")
    require(earliest <= start <= end, f"Stale or invalid runner interval: {name}")
    require(run.get("sourceFilesVerifiedBeforeStart") == run.get("sourceFilesVerifiedAfterExit") == 32,
            f"Actual 32-input verification is missing: {name}")
    require(start <= utc_value(run.get("nativeReportModifiedUtc"), name+" native mtime") <= end,
            f"Native output was not newly written during this run: {name}")
    if native.get("utcTime"):
        require(start <= utc_value(native["utcTime"], name+" native UTC") <= end,
                f"Native UTC is outside its runner interval: {name}")
    command = run.get("command")
    require(isinstance(command, list) and command, f"Missing actual runner command: {name}")
    if name == "compile":
        require(len(command) == 2 and Path(command[0]).name == "validate_unity.sh"
                and command[1] == "compile", "New compilation must run the real PlayerScripts validator")
    else:
        method = {"After": "PocketStrikerMenuBackgroundValidation.StartStaticGradientBatch",
                  "geometry": "PocketStrikerUIArtValidation.ValidateGeometryBatch",
                  "pointer": "PocketStrikerUIArtValidation.StartBatch"}[name]
        require("-executeMethod" in command and method in command
                and "-buildTarget" in command and "iOS" in command,
                f"Actual Editor method or target differs: {name}")
    if name == "After":
        env = run.get("environment", {})
        expected = {"POCKETSTRIKER_MENU_REVIEW_ROOT": str(REVIEW), "POCKETSTRIKER_MENU_REVIEW": "After",
                    "POCKETSTRIKER_MENU_SOURCE_SET": SOURCE_SET, "POCKETSTRIKER_MENU_DYNAMIC_REVIEW": "0",
                    "POCKETSTRIKER_MENU_SOURCE_HEAD": BASE_HEAD}
        require(all(env.get(k) == v for k, v in expected.items()), "Native static runner environment differs")
        require(len(data["runs"]) == 1, "Native receipt must contain only this actual After run")
    return run


def verify(root: Path, *, native_only: bool = False) -> dict:
    from PIL import Image

    review = root / REVIEW
    paths = {"after": review/"After/Runtime/report.json", "parity": review/"After/source-parity.json",
             "nativeReceipt": review/"After/run-receipt.json",
             "manifest": root/"Tools/ArtSources/MenuBackgrounds"/SOURCE_SET/"manifest.json",
             "sourceAudit": review/"source-implementation-audit.json", "aspectAudit": review/"aspect-source-audit.json",
             "sceneWrite": review/"scene-material-write.json", "installation": review/"asset-installation.json"}
    reports = {k: load_json(v) for k, v in paths.items()}
    r, manifest, parity = reports["after"], reports["manifest"], reports["parity"]
    passed_report(r, "Final static native", complete=True)
    require(r.get("staticGradient") is True and all(r.get(k) is False for k in ("baseline", "artRefresh", "previewOnly", "dynamicReview")),
            "Final native evidence is not the complete production static suite")
    require(r.get("sourceHead") == BASE_HEAD and r.get("sourceSet") == SOURCE_SET and r.get("reviewLabel") == "After"
            and r.get("unityVersion") == "6000.5.1f1" and r.get("sourceAssetsUnchanged") is True
            and r.get("externalServicesIsolated") is True, "Final native source/scope differs")
    require(all(number(r, key) == value for key, value in COUNTS.items()), "Final static coverage is incomplete")
    require(all(number(r, key) == 0 for key in ("scrollingChecks", "scrollSpeedChecks", "dynamicFrameChecks",
                                               "nativeEdgeChecks", "identicalCellChecks", "densityChecks")),
            "Static report unexpectedly contains old moving/repeat-unit coverage")
    approved = manifest.get("approvedSource", {})
    require(approved.get("sha256") == APPROVED_SHA and approved.get("dimensions") == [942, 1670]
            and approved.get("libraryVersion") == 3
            and approved.get("libraryFileId") == "libfile_49a5b68988b48191ac3c744444912f9e",
            "Production source differs from the approved latest static image")
    source = bound_file(root, approved)
    with Image.open(source) as image:
        require(image.size == (942, 1670), "Approved image native dimensions differ")
        source_rgba = image.convert("RGBA")
    importer = manifest.get("importerContract", {})
    require(importer == {"pointFilter": True, "wrapU": "Repeat", "wrapV": "Clamp", "mipmaps": False,
                         "npotScale": "None", "compression": "None", "maxTextureSize": 2048,
                         "platformOverrides": False, "preservedGuids": True}, "Production pixel importer contract differs")
    for key in ("shader", "shaderImporter"):
        bound_file(root, manifest[key])
    backgrounds = manifest.get("backgrounds", [])
    require(len(backgrounds) == 6, "Six production backgrounds are required")
    assets = {("null" if b["element"] == "neutral" else b["element"]): b for b in backgrounds}
    require(set(assets) == set(COLORS), "Six semantic palettes are not unique/complete")
    for color, asset in assets.items():
        require(asset["texture"]["sha256"] == APPROVED_SHA and asset["preserveSource"] == (color == "red"),
                "Six source geometry or original Red preservation differs")
        for key in ("texture", "importer", "material", "materialImporter"):
            bound_file(root, asset[key])
    require(parity.get("sourceSet") == SOURCE_SET and parity.get("baseHead") == BASE_HEAD
            and parity.get("allInputsEqual") is True, "Execution source parity does not identify this static integration")
    inputs = parity.get("files", [])
    require(len(inputs) == len({v["path"] for v in inputs}) == 32, "Exactly 32 unique execution inputs are required")
    execution = Path(parity["executionProject"])
    for item in inputs:
        require(item.get("sameExecutionBytes") is True, "A source input differed during actual execution")
        bound_file(root, item)
        bound_file(review/"After/Execution/Source", item)
        bound_file(execution, item)
    for key in ("sourceAudit", "aspectAudit"):
        passed_report(reports[key], key, complete=True)
    require(reports["sourceAudit"]["approvedSourceSha256"] == APPROVED_SHA
            and all(reports["sourceAudit"]["checks"].values()), "Independent production audit has an unresolved finding")
    input_paths = {item["path"] for item in inputs}
    # This historical source audit also hashes documentation which the owner
    # may subsequently update. Only executed production inputs are frozen here.
    for item in reports["sourceAudit"]["auditedInputHashes"]:
        if item["path"] in input_paths:
            bound_file(root, item)
    for item in reports["sourceAudit"]["restorationBackups28"]:
        require(item.get("matchesFrozen") is True, "A frozen restoration source differs")
    scene = root/"Assets/Scene/MainScene/MainMenuScene.unity"
    require(sha256(scene) == reports["sceneWrite"]["outputSHA"] == reports["sourceAudit"]["scene"]["afterSha256"]
            == reports["aspectAudit"]["scene"]["sha256"], "Authored static Scene differs from inspected production bytes")
    require(reports["installation"].get("materialPaletteContractMatches") is True
            and reports["installation"].get("shaderTimeIndependent") is True, "Material/shader installation audit failed")
    run = check_receipt(reports["nativeReceipt"], "After", paths["after"], paths["parity"],
                        utc_value(manifest["utcTime"], "production installation"), r)
    start, end = utc_value(run["startedUtc"], "After start"), utc_value(run["finishedUtc"], "After finish")
    cases_list = r.get("cases", [])
    cases = {(c["viewport"], c["color"]): c for c in cases_list}
    require(len(cases_list) == len(cases) == 24 and set(cases) == {(v, c) for v in VIEWPORTS for c in COLORS},
            "Actual static cases are duplicate/incomplete")
    frames, inventory, uv_error, wait_times = {}, {}, 0.0, []
    for (view, color), case in cases.items():
        w, h = map(int, view.split("x")); expected = expected_uv(w, h); asset = assets[color]
        require(case.get("activeBackgrounds") == 1 and case.get("backgroundIndex") == COLORS.index(color)
                and case.get("focusIsNull") == (color == "null"), "Actual focus/color activation semantics differ")
        require(all(case.get(k) is True for k in ("nativeReturn", "nativeReload", "backgroundCoversViewport",
                                                  "textureRepeat", "sourceRaycastDisabled")), "Actual background/navigation coverage failed")
        require((case.get("textureWidth"), case.get("textureHeight")) == (942, 1670)
                and case.get("textureSha256") == APPROVED_SHA and case.get("texturePath") == asset["texture"]["path"]
                and case.get("textureGuid") == asset["textureGuid"] and case.get("materialPath") == asset["materialPath"]
                and case.get("materialSha256") == asset["material"]["sha256"], "Native screenshot source/material differs")
        require((case.get("filterMode"), case.get("samplerU"), case.get("samplerV"), case.get("shader"))
                == ("Point", "Repeat", "Clamp", "PocketStriker/UI/StaticPixelGradient"), "Native sampler/shader contract differs")
        check_uv(case["uvRect"], expected, view+"/"+color)
        require(abs(case["sampledNativePixels"]["y"]-1670) < .001
                and abs(case["sampledNativePixels"]["x"]-expected["width"]*942) < .01,
                "Native screenshot does not retain the approved complete vertical span")
        scale = h/1670
        require(all(abs(finite(case["screenNativePixelScale"][k], "source pixel scale")-scale) < .00001 for k in ("x", "y")),
                "Native source pixels do not retain equal X/Y display scale")
        require(abs(case["approximateBlockScreenPixelsRange"]["x"]-42*scale) < .001
                and abs(case["approximateBlockScreenPixelsRange"]["y"]-44*scale) < .001,
                "Reported approximate block scale differs from the actual source pixel scale")
        for name in ("staticWait", "nativeReturnStatic", "nativeReloadStatic"):
            pair = case[name]
            uv_error = max(uv_error, check_pair(pair, expected, view+"/"+color+"/"+name, start, end))
            require(pair.get("scrollerEnabledBefore") == pair.get("scrollerEnabledAfter") == [False]*6,
                    "Actual production static scroller became enabled")
            require(abs(finite(pair.get("maxUvError"), "reported raw UV error")) < .00001,
                    "Native all-six UV observation failed")
        wait = case["staticWait"]
        require(number(wait, "frames", positive=True) >= 2 and finite(wait["realtimeElapsed"], "wait real duration") >= .6
                and finite(wait["scaledElapsed"], "wait scaled duration") >= .6
                and wait.get("modelAnimatorAdvanced") is True and finite(wait["animatorSpeed"], "model speed") > 0
                and abs(wait["animatorNormalizedTimeAfter"]-wait["animatorNormalizedTimeBefore"]) > .001,
                "Static background proof froze natural model animation or lacks a real wait")
        require(len(wait["uiAnimatorNames"]) == len(wait["uiAnimatorEnabledBefore"]) == len(wait["uiAnimatorEnabledAfter"]) > 0
                and wait["uiAnimatorEnabledBefore"] == wait["uiAnimatorEnabledAfter"], "Natural UI Animator enabled states changed")
        wait_times.append(wait["realtimeElapsed"])
        for suffix in ("home", "reloaded", "scene-reloaded"):
            path = review/"After/Runtime"/view/f"{color}-{suffix}.png"
            require(path.is_file(), f"Actual complete native screenshot is missing: {path}")
            with Image.open(path) as image:
                require(image.size == (w, h), f"Actual screenshot dimensions differ: {path}")
            inventory[str(path.relative_to(root))] = sha256(path)
            if suffix == "home":
                require(Path(case["screenshot"]).name == path.name and Path(case["screenshot"]).parent.name == view,
                        "Case screenshot does not identify the canonical native frame")
                frames[(view, color)] = path
            elif suffix == "reloaded":
                require(Path(case["reloadedScreenshot"]).name == path.name, "HOME reconstruction screenshot differs")
    reloads = r.get("staticSceneReloads", [])
    require(len(reloads) == 4 and {s["viewport"] for s in reloads} == set(VIEWPORTS), "Actual Single Scene reload coverage is incomplete")
    for event in reloads:
        require(event.get("scenePath") == "Assets/Scene/MainScene/MainMenuScene.unity"
                and event.get("productionSceneReplaced") is True and event.get("serviceIsolationRetained") is True,
                "Actual scene did not reload with retained local isolation")
        w, h = map(int, event["viewport"].split("x"))
        uv_error = max(uv_error, check_pair(event, expected_uv(w, h), "actual Single reload", start, end))
    observations = r.get("staticObservations", [])
    purposes = ("natural animation and stationary background", "native collection selection and Return",
                "native HOME UI reconstruction", "stationary background after scene reload")
    indexed_observations = {(o["viewport"], o["color"], o["purpose"]): o for o in observations}
    require(len(observations) == len(indexed_observations) == 96
            and set(indexed_observations) == {(v, c, p) for v in VIEWPORTS for c in COLORS for p in purposes},
            "All 96 natural/native/after-scene static observations are required")
    for (view, color, purpose), observation in indexed_observations.items():
        w, h = map(int, view.split("x"))
        uv_error = max(uv_error, check_pair(observation, expected_uv(w, h), view+"/"+color+"/"+purpose, start, end))
        require(observation.get("scrollerEnabledBefore") == observation.get("scrollerEnabledAfter") == [False]*6,
                "Production scroller became enabled during a recorded static observation")
        require(abs(finite(observation.get("maxUvError"), "reported observation error")) < .00001,
                "A recorded static observation reports drift")
        if purpose == purposes[3]:
            require(number(observation, "frames", positive=True) >= 2 and observation["realtimeElapsed"] >= .6
                    and observation["scaledElapsed"] >= .6 and observation.get("modelAnimatorAdvanced") is True
                    and observation["uiAnimatorEnabledBefore"] == observation["uiAnimatorEnabledAfter"],
                    "Actual scene reload lacks a stationary wait with continuing natural animation")
        elif purpose in purposes[:3]:
            key = {purposes[0]: "staticWait", purposes[1]: "nativeReturnStatic", purposes[2]: "nativeReloadStatic"}[purpose]
            require(observation == cases[(view, color)][key], "Native case and global raw observation records differ")
    palettes = r.get("staticPaletteSamples", [])
    require(len(palettes) == 6 and {p["color"] for p in palettes} == set(COLORS), "Actual six-palette GPU samples are incomplete")
    max_gpu_error = 0.0
    for gpu in palettes:
        color, asset = gpu["color"], assets[gpu["color"]]
        require(gpu.get("sourcePngSha256") == APPROVED_SHA and gpu.get("materialSha256") == asset["material"]["sha256"]
                and gpu.get("materialPath") == asset["materialPath"] and gpu.get("preserveSource") == (color == "red")
                and gpu.get("colorSpace") == "Gamma", "Actual GPU probe uses another source/material/color space")
        palette = [gpu["displaySrgbPalette"][k] for k in ("x", "y", "z")]
        require(all(abs(a-b) < .000001 for a, b in zip(palette, asset["srgbVector"])), "GPU palette differs from the approved material")
        samples = gpu.get("samples", [])
        require(len(samples) == 28, "Actual GPU probe does not contain all 28 recorded source/display pixels")
        path = review/"After/Runtime/375x667"/f"{color}-palette-gpu-only.png"
        require(Path(gpu["screenshot"]).name == path.name and path.is_file(), "Independent GPU diagnostic capture is missing")
        with Image.open(path) as image:
            require(image.size == (375, 667), "Independent GPU diagnostic dimensions differ")
            image = image.convert("RGBA")
            local_max = 0.0
            for sample in samples:
                sp, tp = sample["screenPixel"], sample["sourcePixel"]
                source_pixel = source_rgba.getpixel((tp["x"], 1669-tp["y"]))
                rendered = image.getpixel((sp["x"], 666-sp["y"]))
                require(tuple(sample["sourceRgba"][k] for k in ("r", "g", "b", "a")) == source_pixel
                        and tuple(sample["actualRgba"][k] for k in ("r", "g", "b", "a")) == rendered,
                        "Independent GPU probe records do not match actual PNG pixels")
                expected_rgb = source_pixel[:3] if color == "red" else tuple(source_pixel[0]*v for v in asset["srgbVector"])
                require(all(abs(sample["expectedDisplaySrgb8Bit"][k]-v) < .00001
                            for k, v in zip(("x", "y", "z"), expected_rgb)), "GPU expected RGB is not independent source-R palette arithmetic")
                error = max(abs(a-b) for a, b in zip(rendered[:3], expected_rgb))
                require(error <= .50001 and abs(error-sample["maxChannelError8Bit"]) < .00001,
                        "Actual GPU rendered color differs from its recorded/independent expectation")
                local_max = max(local_max, error)
            require(abs(local_max-gpu["maxChannelError8Bit"]) < .00001, "GPU maximum error is not the actual sample maximum")
            max_gpu_error = max(max_gpu_error, local_max)
        inventory[str(path.relative_to(root))] = sha256(path)
    actual_pngs = {str(p.relative_to(root)) for p in (review/"After/Runtime").rglob("*.png")}
    require(len(inventory) == 78 and actual_pngs == set(inventory), "Final native PNG inventory differs from 72 real UI frames plus 6 GPU diagnostics")
    recorded_pngs = r.get("screenshots", [])
    require(len(recorded_pngs) == len(set(recorded_pngs)) == 78, "Native report capture inventory differs")
    for recorded in recorded_pngs:
        original = Path(recorded)
        canonical = review/"After/Runtime"/original.parent.name/original.name
        require(str(canonical.relative_to(root)) in inventory and original.is_file()
                and sha256(original) == sha256(canonical), "Canonical screenshot bytes differ from the actual recorded native file")
    runs = {"After": run}
    if not native_only:
        for key, filename in (("geometry", "geometry-report.json"), ("pointer", "ui-pointer-report.json"), ("compile", "compile-iOS-report.json")):
            paths[key] = review/"Regression"/filename
            reports[key] = load_json(paths[key])
            passed_report(reports[key], "New static "+key)
            require(reports[key].get("unityVersion") == r["unityVersion"], "New regression ran with another Unity version")
        paths["regressionReceipt"] = review/"Regression/run-receipts.json"
        receipts = load_json(paths["regressionReceipt"])
        require(len(receipts.get("runs", [])) == 3 and {v["name"] for v in receipts["runs"]} == {"geometry", "pointer", "compile"},
                "All three new regression receipts are required")
        previous = end
        for name in ("geometry", "pointer", "compile"):
            runs[name] = check_receipt(receipts, name, paths[name], paths["parity"], previous, reports[name])
            previous = utc_value(runs[name]["finishedUtc"], name+" finish")
        g, p, c = (reports[k] for k in ("geometry", "pointer", "compile"))
        require(g.get("independentGeometryPassed") is True and g.get("fixtureObjectsRestored") is True
                and g.get("layoutCases") == 236 and g.get("compositions") == 88 and g.get("stageCardCases") == 432,
                "New independent full layout geometry validation is incomplete")
        if g.get("sourceLayoutPassed") is False:
            require(g.get("sceneDirtyOnlyFinding") is True and g.get("sourceLayoutErrors") == 1
                    and g["fixtureBefore"]["objectFingerprint"] == g["fixtureAfter"]["objectFingerprint"]
                    and g["fixtureBefore"]["previewScenes"] == g["fixtureAfter"]["previewScenes"],
                    "Original layout failure exceeds the documented isolated empty-scene dirty flag")
        require(p.get("viewportCases") == 4 and p.get("settingsTabCases") == 48 and p.get("raycastChecks") == 68,
                "New native UI interaction coverage is incomplete")
        require(c.get("expectedUnityVersion") == "6000.5.1f1" and c.get("activeBuildTarget") == "iOS"
                and c.get("portraitOnly") is True and c.get("defaultOrientation") == c.get("runtimeOrientation") == "Portrait",
                "Compilation is not the required local iOS Portrait PlayerScripts check")
    return {"root": root, "review": review, "paths": paths, "reports": reports, "cases": cases,
            "frames": frames, "inventory": inventory, "runs": runs, "rawUvPairs": len(observations)*6,
            "maxRawUvDelta": uv_error, "waitRange": [min(wait_times), max(wait_times)],
            "gpuPixels": 168, "maxGpuError8Bit": max_gpu_error, "parityCount": 32}


class Builder(PdfBuilder):
    def page(self, title: str, subtitle: str = "") -> None:
        from reportlab.lib.colors import HexColor
        if self.page_no:
            self.canvas.showPage()
        self.page_no += 1
        self.canvas.setFillColor(HexColor("#FAF8F3"))
        self.canvas.rect(0, 0, self.WIDTH, self.HEIGHT, fill=1, stroke=0)
        self.text("POCKETSTRIKER / STATIC PIXEL GRADIENT", 34, 770, size=8, color="#647383")
        self.text(title, 34, 746, size=20)
        self.paragraph(subtitle, 34, 729, 544, size=9.5, bottom=686)
        self.canvas.setStrokeColor(HexColor("#D8D4CB"))
        self.canvas.line(34, 46, 578, 46)
        self.text("完整 Unity Editor 实景 | 本地验证，非真机认证", 34, 29, size=8, color="#6F7780")
        self.text(f"{self.page_no} / 3", 552, 29, size=8, color="#6F7780")

    def finish(self) -> None:
        require(self.page_no == 3, "Static gradient review must have three pages")
        self.canvas.save()


def build(e: dict, output: Path, font: Path, quality: int) -> None:
    b = Builder(output, font, quality)
    b.canvas.setTitle("PocketStriker 静态方形像素纵向渐变实际验收")
    b.page("静态像素渐变 / 六色手机", "已批准红至近黑 v3 全高取景；六色保留同一方形像素几何。背景静止，角色与界面动画继续。")
    b.text("375x667 | 原生完整主界面截图 | 五元素 + Null", 34, 683, size=9, color="#65717B")
    for n, color in enumerate(COLORS):
        x, y = 46+(n%3)*181, 379 if n < 3 else 78
        b.text(LABELS[color][0], x, y+282, size=11, color=ACCENTS[color])
        b.image(e["frames"][("375x667", color)], x, y, 155, 155*667/375)
    b.text("Null 为真实无焦点状态，保留红色角色预览；其背景使用中性灰配色。", 34, 61, size=8.5, color="#65717B")
    b.page("静态像素渐变 / 六色平板", "768x1024 原生完整画面：保留纵向全部渐变端点，横向以 U Repeat 补足宽度；方块保持等比例。")
    b.text("768x1024 | 原生完整主界面截图 | 横向扩展，无纵向重复", 34, 683, size=9, color="#65717B")
    for n, color in enumerate(COLORS):
        x, y = 46+(n%3)*181, 425 if n < 3 else 130
        b.text(LABELS[color][0], x, y+220, size=11, color=ACCENTS[color])
        b.image(e["frames"][("768x1024", color)], x, y, 155, 155*1024/768)
    b.paragraph("六张 PNG 与获批 942x1670 原图字节相同。Red 保留原 RGB，其余专用材质以原图 R 强度映射元素色。Point 采样保留原像素块；源边距约42-44px，非严格重复单位。", 34, 104, 544, size=9, bottom=54)
    b.page("实际测试结果与范围", "新静态场景、原生导航和本轮三项回归，均绑定真实运行收据与同一 32 文件源清单。")
    r, g, p, c = (e["reports"][k] for k in ("after", "geometry", "pointer", "compile"))
    rows = [
        ("实际画面与比例", f"24状态 / 4比例 / 72主界面截图；另6独立GPU诊断图。375x667、390x844、540x960、768x1024"),
        ("静止与自然动画", f"{r['staticWaitChecks']}次实际等待，最短{e['waitRange'][0]:.3f}s；{r['modelAnimationChecks']}次角色动画推进；背景UV最大变化{e['maxRawUvDelta']:.6g}"),
        ("返回、HOME与场景重载", f"{r['staticNavigationChecks']}次原生Return/HOME前后；{r['actualSceneReloads']}次真实Single场景重载 / {r['staticSceneReloadChecks']}色状态；六背景UV保持全高"),
        ("比例与故障恢复", f"{r['resizeChecks']}次尺寸切换；零矩形/晚到纹理各{r['zeroRectRecoveryChecks']}次；{r['staticBadUvRecoveryChecks']}次坏UV；{r['staticScrollerRestartGuardChecks']}次重启守护；{r['legacyDefaultModeChecks']}次旧默认模式"),
        ("本轮真实触控回调", f"主菜单{r['pointerClicks']}次Editor原生pointer；新UI回归{p['settingsTabCases']}设置页签 / {p['raycastChecks']}raycast"),
        ("独立GPU配色采样", f"6色 x 28像素 = {e['gpuPixels']}实际采样；Gamma Editor最大RGB误差{e['maxGpuError8Bit']:.3f}/255；独立白RawImage诊断，非HOME遮挡区比较"),
        ("新独立几何回归", f"{g['layoutCases']}布局 / {g['compositions']}组合 / {g['stageCardCases']}卡片；fixture对象指纹与预览场景数量恢复"),
        ("新iOS脚本编译", f"{c['unityVersion']} / iOS / Portrait，PlayerScripts通过；不代表签名IPA或真机着色器认证"),
    ]
    y = b.table(rows, 700, size=8.8)-15
    b.text("实际资源与显示方式", 34, y, size=11)
    y = b.paragraph("六资源GUID保留；六专用材质、同对象OffsetScrolling关闭；全高UV固定y=0、height=1，横向居中且等比例。约43源px的块在本轮四比例约17.17-26.37屏幕px，实际源边距42-44px。获批黑尾1-2码值保留。横向源边RGB不完全相等，不声称重复接缝逐像素相同。", 34, y-8, 544, size=9)-14
    b.text("本轮真实执行时间 (UTC)", 34, y, size=11)
    y = b.paragraph("\n".join(f"{name}: {e['runs'][name]['startedUtc']} → {e['runs'][name]['finishedUtc']}" for name in ("After", "geometry", "pointer", "compile")), 34, y-8, 544, size=8)-14
    b.text("验证边界", 34, y, size=11)
    limitation = "Editor原生pointer验证回调与raycast，未认证真机触摸/安全区/性能/平台GPU。GPU配色采样仅Gamma Editor，本轮未执行Linear项目或实际iOS设备着色。使用本地离线账户与Addressables适配；未测试登录、线上账户/IAP/广告。此轮仅本地，无push/publish。"
    y = b.paragraph(limitation, 34, y-8, 544, size=8.8)-10
    if g.get("sourceLayoutPassed") is False:
        y = b.paragraph(f"原通用source-layout报告仍为false，含{g['sourceLayoutErrors']}项隔离空场景dirty标记错误；本轮仅独立几何与对象/组件指纹恢复通过。", 34, y, 544, size=8.4)-10
    b.paragraph(f"证据：Logs/MenuStaticPixelGradientReview/After/Runtime、Regression及运行收据；生产资源清单StaticPixelGradient-v1。基线{BASE_HEAD[:9]}，执行前后32文件哈希一致。", 34, y, 544, size=8, color="#65717B")
    b.finish()


def render_and_check(output: Path, directory: Path, dpi: int) -> dict:
    import pymupdf
    from pypdf import PdfReader
    reader = PdfReader(output)
    require(len(reader.pages) == 3, "Written static PDF has the wrong page count")
    texts = [p.extract_text() or "" for p in reader.pages]
    require(all(f"{i} / 3" in text for i, text in enumerate(texts, 1)), "PDF page numbering failed")
    require("72" in texts[2] and "Gamma" in texts[2] and "42-44" in texts[1]
            and "false" in texts[2], "Actual static counts or limitations are missing")
    require(any(ref.get_object().get("/FontDescriptor") and "/FontFile2" in ref.get_object()["/FontDescriptor"].get_object()
                for page in reader.pages for ref in page["/Resources"]["/Font"].get_object().values()), "Chinese TrueType font is not embedded")
    directory.mkdir(parents=True, exist_ok=True)
    renders = []
    with pymupdf.open(output) as doc:
        for i, page in enumerate(doc, 1):
            for block in page.get_text("dict")["blocks"]:
                if block.get("type") == 0:
                    x0, y0, x1, y1 = block["bbox"]
                    require(min(x0, y0) >= 0 and x1 <= page.rect.width and y1 <= page.rect.height, "Actual PDF text extends beyond page bounds")
            path = directory/f"page-{i:02d}.png"
            page.get_pixmap(matrix=pymupdf.Matrix(dpi/72, dpi/72), alpha=False).save(path)
            renders.append(str(path))
    return {"pages": 3, "embeddedChineseTrueType": True, "renderedPages": renders, "visualInspectionRequired": True}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path, default=ROOT/"output/pdf"/OUTPUT_NAME)
    parser.add_argument("--render-dir", type=Path, default=ROOT/"tmp/pdfs/menu-static-gradient")
    parser.add_argument("--font", type=Path, default=FONT_DEFAULT)
    parser.add_argument("--jpeg-quality", type=int, default=88)
    parser.add_argument("--render-dpi", type=int, default=120)
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--validate-native-only", action="store_true")
    args = parser.parse_args()
    root, output = args.root.resolve(), args.output.resolve()
    require(output.name == OUTPUT_NAME, "Use the new independent static-gradient output filename")
    require(70 <= args.jpeg_quality <= 95 and 72 <= args.render_dpi <= 200, "Invalid JPEG/render quality")
    protected = {name: sha256(root/"output/pdf"/name) for name in PROTECTED}
    require(protected == PROTECTED, "A previous reviewed PDF changed")
    e = verify(root, native_only=args.validate_native_only)
    if args.validate_only or args.validate_native_only:
        print(json.dumps({"validated": True, "nativeOnly": args.validate_native_only,
                          "newRegressionsVerified": not args.validate_native_only, "sourceSet": SOURCE_SET,
                          "cases": 24, "actualHomeFrames": 72, "gpuDiagnosticFrames": 6,
                          "sourceInputs": 32, "verifiedRuns": list(e["runs"]),
                          "rawUvPairs": e["rawUvPairs"], "maxRawUvDelta": e["maxRawUvDelta"],
                          "waitRangeSeconds": e["waitRange"], "gpuPixels": e["gpuPixels"],
                          "maxGpuError8Bit": e["maxGpuError8Bit"]}, ensure_ascii=False, indent=2))
        return 0
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.stem+".building.pdf")
    try:
        build(e, temporary, args.font, args.jpeg_quality)
        require(temporary.stat().st_size < 10_000_000, "PDF exceeds the 10 MB limit")
        render = render_and_check(temporary, args.render_dir.resolve(), args.render_dpi)
        require({name: sha256(root/"output/pdf"/name) for name in PROTECTED} == protected, "A previous PDF changed during authoring")
        temporary.replace(output)
        receipt = {"createdUtc": datetime.now(timezone.utc).isoformat(), "output": str(output),
                   "sha256": sha256(output), "bytes": output.stat().st_size, "pages": 3,
                   "reports": {name: {"path": str(path), "sha256": sha256(path)} for name, path in e["paths"].items()},
                   "sourceSet": SOURCE_SET, "sourceInputs": 32, "nativeScreenshotInventory": e["inventory"],
                   "embeddedFrames": [{"viewport": view, "color": color, "path": str(e["frames"][(view, color)]),
                                       "sha256": sha256(e["frames"][(view, color)])}
                                      for view in ("375x667", "768x1024") for color in COLORS],
                   "jpegQuality": args.jpeg_quality, "fullFramesPreserved": True, "render": render,
                   "measurements": {k: e[k] for k in ("rawUvPairs", "maxRawUvDelta", "waitRange", "gpuPixels", "maxGpuError8Bit")},
                   "actualRunnerReceipts": e["runs"], "protectedPreviousPdfSha256": protected,
                   "scope": "Local Editor native static evidence, independent Gamma GPU probes and fresh PlayerScripts compile; no device/IPA/network/Library verification"}
        output.with_suffix(".receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
        print(json.dumps({"output": str(output), "sha256": receipt["sha256"], "bytes": receipt["bytes"],
                          "pages": 3, "renderedPages": render["renderedPages"]}, ensure_ascii=False, indent=2))
    finally:
        if temporary.exists():
            temporary.unlink()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, KeyError, ValueError, OSError) as error:
        print(f"Static-gradient evidence is not ready: {error}", file=sys.stderr)
        raise SystemExit(2)

#!/usr/bin/env python3
"""Prepare a separate four-page V5-to-V6 native menu review.

Read-only preflight: ``python Tools/Art/build_menu_simple_grid_review_pdf.py --validate-only``.
Before the first authoring run, invoke the PDF skill operation marker, then inspect all
four rendered pages. This builder never runs Unity or uploads to Library. The
previous eight-page PDF and its Library identity are protected from replacement.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import sys

from build_menu_background_review_pdf import (
    ACCENTS, BASE_HEAD, COLORS, EvidenceError, FONT_DEFAULT, LABELS, PdfBuilder,
    VIEWPORTS, case_index, compare_controls, load_json, number, passed_report,
    require, sha256, utc_value,
)

ROOT = Path(__file__).resolve().parents[2]
REVIEW = Path("Logs/MenuBackgroundSimplifyReview")
OLD_PDF = Path("output/pdf/PocketStriker-Menu-Backgrounds-20261003.pdf")
OUTPUT_NAME = "PocketStriker-Menu-Simple-Grid-20261003.pdf"
V5_REPORT_HASHES = {
    "Runtime/report.json": "4257ee333e0bd3d2bff9e1be9079315b7e9c753a30546577360f9083714afd25",
    "build.json": "d07e4afee6a3bb327e87b9b954f32d29a423bb167e2228ee9a650ce22a2b39da",
    "source-parity.json": "754f43fe27730560fc6e3930a5a25677dd11855e1765e1c00fa05a15ea38b28d",
}
UNIT = 256
GRID = 6
RUNTIME_SIZE = UNIT * GRID
# V5's authoring geometry has staggered rows. These are declared source pitches,
# not a computer-vision count of visible, unobstructed diamonds in the native UI.
V5_ROW_PITCH_X = 156.5
V5_ROW_PITCH_Y = 78.25
STYLE = "格子更疏，每格只保留同一个空心像素菱形，格内与格间留白。五色与 Null 沿用柔和低饱和配色。"


def assets_by_color(build: dict) -> dict:
    textures = build.get("textures", [])
    result = {("null" if item.get("theme") == "neutral" else item.get("theme")): item for item in textures}
    require(len(textures) == len(result) == 6 and set(result) == set(COLORS), "Build does not contain six unique mapped textures")
    return result


def parity_files(report: dict) -> dict:
    files = report.get("files", [])
    result = {entry["path"]: entry for entry in files}
    require(len(files) == len(result) == 17, "Execution parity must identify 17 unique files")
    require(report.get("baseHead") == BASE_HEAD, "Execution parity has a different base HEAD")
    return result


def native_frames(root: Path, directory: Path, report: dict, stage: str) -> dict:
    from PIL import Image
    frames = {}
    for case in report["cases"]:
        view, color = case["viewport"], case["color"]
        path = directory / view / f"{color}-home.png"
        require(path.is_file(), f"Missing {stage} native frame: {path}")
        require(case["screenshot"].endswith(f"/{view}/{color}-home.png"), f"Invalid native frame reference: {stage}/{view}/{color}")
        with Image.open(path) as image:
            require(image.size == tuple(map(int, view.split("x"))), f"Wrong native backbuffer size: {path}")
        original = Path(case["screenshot"])
        if original.is_file():
            require(sha256(original) == sha256(path), f"Frozen/native screenshot bytes differ: {path}")
        frames[(stage, view, color)] = path
    return frames


def verify_capture_inventory(directory: Path, report: dict) -> None:
    from PIL import Image
    wanted = {f"{view}/{color}-{suffix}.png" for view in VIEWPORTS for color in COLORS
              for suffix in ("home", "reloaded", "wrap-before", "wrap-after")}
    actual = {str(path.relative_to(directory)) for path in directory.glob("*/*.png")}
    require(actual == wanted and len(actual) == 96, f"Native screenshot inventory is not the full 96 frames: {directory}")
    referenced = report.get("screenshots", [])
    require(isinstance(referenced, list) and len(referenced) == 96
            and {"/".join(Path(path).parts[-2:]) for path in referenced} == wanted,
            f"Native report does not reference exactly the actual 96 screenshots: {directory}")
    for native_reference in referenced:
        relative = Path(*Path(native_reference).parts[-2:])
        path = directory / relative
        with Image.open(path) as image:
            require(image.size == tuple(map(int, relative.parts[0].split("x"))), f"Native full-frame dimensions differ: {path}")
        original = Path(native_reference)
        if original.is_file():
            require(sha256(original) == sha256(path), f"Native screenshot bytes differ from their capture source: {path}")


def sampled_area_equivalent(case: dict, *, v6: bool) -> float:
    rect = case["uvRect"]
    area = float(case["textureWidth"]) * float(case["textureHeight"]) * float(rect["width"]) * float(rect["height"])
    pitch_area = UNIT * UNIT if v6 else V5_ROW_PITCH_X * V5_ROW_PITCH_Y
    return area / pitch_area


def verify_grid_pixels(path: Path) -> dict:
    """Independently check finished PNG bytes, rather than relying on flags."""
    from PIL import Image
    with Image.open(path) as image:
        require(image.size == (RUNTIME_SIZE, RUNTIME_SIZE), f"V6 runtime must be a {GRID}x{GRID} array of {UNIT}px cells: {path}")
        image = image.convert("RGBA")
        first = image.crop((0, 0, UNIT, UNIT)).tobytes()
        mismatches = sum(
            image.crop((x * UNIT, y * UNIT, (x + 1) * UNIT, (y + 1) * UNIT)).tobytes() != first
            for y in range(GRID) for x in range(GRID)
        )
        require(mismatches == 0, f"V6 cells differ within one color: {path}")
        horizontal = image.crop((0, 0, 1, image.height)).tobytes() == image.crop((image.width - 1, 0, image.width, image.height)).tobytes()
        vertical = image.crop((0, 0, image.width, 1)).tobytes() == image.crop((0, image.height - 1, image.width, image.height)).tobytes()
        require(horizontal and vertical, f"Actual V6 PNG seams differ: {path}")
    return {"unitPixels": UNIT, "columns": GRID, "rows": GRID, "cellsCompared": GRID * GRID,
            "mismatchingCells": mismatches, "horizontalSeamExact": horizontal, "verticalSeamExact": vertical,
            "unitRgbaSha256": hashlib.sha256(first).hexdigest()}


def verify_fixture_grid_report(report: dict, assets: dict, grid_checks: dict, cases: dict) -> None:
    """Bind native PNG-decode/unit/density results to actual current bytes."""
    require(number(report, "unitSize") == UNIT and number(report, "nativeEdgeChecks") == 12
            and number(report, "identicalCellChecks") == 6 and number(report, "densityChecks") == 24,
            "Native final run did not complete the unit/edge/density suite")
    tiles = report.get("nativeTileChecks", [])
    require(isinstance(tiles, list) and len(tiles) == 6
            and [tile.get("color") for tile in tiles] == list(COLORS), "Native PNG tile inventory/order is incomplete")
    for index, color in enumerate(COLORS):
        tile, asset = tiles[index], assets[color]
        require(tile.get("texturePath") == asset["destination"] and tile.get("pngSha256") == asset["runtimeSha256"],
                f"Native unit check decoded another PNG: {color}")
        require(tile.get("unitRgbaSha256") == grid_checks[color]["unitRgbaSha256"], f"Native and independent RGBA unit bytes differ: {color}")
        require(tile.get("cellByteFormat") == "RGBA32 (four 8-bit channels), top-left row order", f"Native RGBA byte order differs: {color}")
        require(tile.get("width") == tile.get("height") == RUNTIME_SIZE and tile.get("unitSize") == UNIT
                and tile.get("rows") == tile.get("columns") == GRID and tile.get("uniqueCells") == 1
                and tile.get("cellByteCount") == UNIT * UNIT * 4, f"Native unit dimensions/counts differ: {color}")
        require(all(tile.get(key) is True for key in ("horizontalEdgesEqual", "verticalEdgesEqual", "everyCellIdentical")),
                f"Native tile edges/cells failed: {color}")
        for view in VIEWPORTS:
            case = cases[(view, color)]
            require(case.get("unitSize") == UNIT and case.get("cellRows") == case.get("cellColumns") == GRID
                    and case.get("nativeTileCheckIndex") == index and case.get("unitRgbaSha256") == tile["unitRgbaSha256"],
                    f"Native rendered case has another unit: {view}/{color}")
            visible_x = RUNTIME_SIZE * float(case["uvRect"]["width"]) / UNIT
            visible_y = RUNTIME_SIZE * float(case["uvRect"]["height"]) / UNIT
            expected = {"visibleCellsX": visible_x, "visibleCellsY": visible_y,
                        "screenCellPixelsX": float(case["backgroundScreenRect"]["width"]) / visible_x,
                        "screenCellPixelsY": float(case["backgroundScreenRect"]["height"]) / visible_y}
            for key, value in expected.items():
                require(abs(float(case.get(key, -1)) - value) < 0.001, f"Native cell density differs from actual UV/rectangle: {view}/{color}/{key}")
            require(abs(expected["screenCellPixelsX"] - expected["screenCellPixelsY"]) < 0.1,
                    f"Native square cells are stretched: {view}/{color}")


def verify(root: Path) -> dict:
    review = root / REVIEW
    before_dir = review / "Before-v5"
    paths = {
        "snapshot": before_dir / "baseline-snapshot.json",
        "before": before_dir / "Runtime/report.json",
        "before_build": before_dir / "build.json",
        "before_parity": before_dir / "source-parity.json",
        "after": review / "After/Runtime/report.json",
        "build": review / "build.json",
        "parity": review / "After/source-parity.json",
        "after_run": review / "After/run-receipt.json",
        "prior_geometry": root / "Logs/MenuBackgroundReview/Regression/geometry-report.json",
        "prior_pointer": root / "Logs/MenuBackgroundReview/Regression/ui-pointer-report.json",
        "prior_compile": root / "Logs/MenuBackgroundReview/Regression/compile-iOS-report.json",
        "prior_runs": root / "Logs/MenuBackgroundReview/Regression/run-receipts.json",
        "prior_pdf_receipt": root / OLD_PDF.with_suffix(".receipt.json"),
    }
    reports = {name: load_json(path) for name, path in paths.items()}
    snapshot = reports["snapshot"]
    require(snapshot.get("baseHead") == BASE_HEAD and snapshot.get("sourceSet") == "Pattern-v5", "Before snapshot is not the preceding final V5")
    require(snapshot.get("beforeNativeBaselineFlag") is False, "Frozen native baseline flag was changed")
    require(snapshot.get("sourceScreenshotCount") == 96, "V5 snapshot does not identify all 96 native frames")
    for name, digest in V5_REPORT_HASHES.items():
        require(snapshot.get("reports", {}).get(name, {}).get("sha256") == digest and sha256(before_dir / name) == digest,
                f"V5 original report bytes were not preserved: {name}")
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, check=True, capture_output=True, text=True).stdout.strip()
    require(head == BASE_HEAD, "Current HEAD differs from the reviewed local source base")
    for name in ("before", "after"):
        report = reports[name]
        passed_report(report, name, complete=True)
        require(report.get("baseline") is False, f"{name}: do not relabel a native V5/final report as baseline=true")
        require(report.get("sourceHead") in (BASE_HEAD, BASE_HEAD[:9]), f"{name}: different source HEAD")
        require(report.get("externalServicesIsolated") is True and report.get("sourceAssetsUnchanged") is True,
                f"{name}: runtime source preservation or service isolation failed")
        require(report.get("previewOnly") is False, f"{name}: preview cannot replace a full 24-case run")
    require(utc_value(reports["after"]["utcTime"], "After") > utc_value(snapshot["utc"], "V5 freeze"), "After predates the V5 freeze")
    require(reports["after"].get("artRefresh") is False, "V6 requires the final full callback suite, not an art-refresh run")
    expected_counts = {"aspectChecks": 90, "preservedPhaseChecks": 98, "resizeChecks": 30,
                       "zeroRectRecoveryChecks": 6, "lateTextureRecoveryChecks": 6}
    for key, value in expected_counts.items():
        require(number(reports["after"], key) == value, f"Final V6 full callback coverage is incomplete: {key}")
    final_runs = reports["after_run"].get("runs", [])
    require(isinstance(final_runs, list) and len(final_runs) == 1 and final_runs[0].get("name") == "After",
            "Final V6 receipt must contain exactly one native After run")
    run = final_runs[0]
    require(run.get("exitCode") == 0 and run.get("sourceBaseHead", run.get("baseHead")) == BASE_HEAD,
            "Final V6 native runner receipt has a failed exit or another source HEAD")
    require(run.get("sourceParityManifestSha256") == sha256(paths["parity"])
            and run.get("nativeReportSha256") == sha256(paths["after"]), "Final V6 runner receipt identifies other source/report bytes")
    started = utc_value(run.get("startedUtc"), "V6 runner start")
    finished = utc_value(run.get("finishedUtc"), "V6 runner finish")
    report_time = utc_value(reports["after"]["utcTime"], "V6 native report")
    require(started >= utc_value(snapshot["utc"], "V5 freeze") and started <= report_time <= finished,
            "Final V6 runner/native report times are inconsistent with the new frozen baseline")
    require(isinstance(run.get("command"), list) and bool(run["command"]), "Final V6 runner command is missing")
    command = run["command"]
    require("-executeMethod" in command and "PocketStrikerMenuBackgroundValidation.StartAfterBatch" in command
            and "-buildTarget" in command and "iOS" in command, "Final V6 receipt did not execute the full native background suite for iOS target")
    environment = run.get("environment", {})
    require(environment.get("POCKETSTRIKER_MENU_UNIT_SIZE") == str(UNIT)
            and environment.get("POCKETSTRIKER_MENU_SOURCE_SET") == "Pattern-v6"
            and environment.get("POCKETSTRIKER_MENU_REVIEW") == "After"
            and environment.get("POCKETSTRIKER_MENU_REVIEW_ROOT") == str(REVIEW)
            and environment.get("POCKETSTRIKER_MENU_SOURCE_HEAD") == BASE_HEAD,
            "Final V6 receipt environment identifies other source, units or output")
    before = case_index(reports["before"], "Frozen V5")
    after = case_index(reports["after"], "Final V6")
    comparison = compare_controls(before, after)
    require(reports["before_build"].get("sourceSet") == "Pattern-v5", "Wrong V5 source set")
    require(reports["build"].get("sourceSet") == "Pattern-v6", "Final build is not selected Pattern-v6")
    old_assets, new_assets = assets_by_color(reports["before_build"]), assets_by_color(reports["build"])
    old_parity, new_parity = parity_files(reports["before_parity"]), parity_files(reports["parity"])
    require(set(old_parity) == set(new_parity), "V6 execution source inventory differs from V5")
    production = {item["destination"] + ".meta" for item in old_assets.values()} | {
        "Assets/Scene/MainScene/MainMenuScene.unity", "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs",
        "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs.meta",
    }
    require(len(production) == 9, "Production equality scope must identify exactly nine control files")
    for path in production:
        require(path in new_parity and new_parity[path]["sha256"] == old_parity[path]["sha256"] == sha256(root / path),
                f"Production code/config changed; prior regressions cannot be reused: {path}")
    for path, entry in new_parity.items():
        require(entry.get("sameExecutionBytes") is True and sha256(root / path) == entry["sha256"], f"Final execution parity is stale: {path}")
        execution = Path(reports["parity"]["executionProject"]) / path
        if execution.is_file():
            require(sha256(execution) == entry["sha256"], f"Execution copy changed: {path}")

    backup_manifest_path = root / snapshot["runtimeBackup"]
    require(sha256(backup_manifest_path) == snapshot["runtimeBackupManifestSha256"], "V5 runtime backup manifest changed")
    backup_manifest = load_json(backup_manifest_path)
    paths["backup_manifest"] = backup_manifest_path
    backup_rows = {item["path"]: item for item in backup_manifest.get("runtimeFiles", [])}
    require(len(backup_rows) == 12, "V5 runtime backup does not contain six PNGs and six metadata files")
    for path, entry in backup_rows.items():
        backup = backup_manifest_path.parent / "runtime" / Path(path).name
        require(backup.is_file() and sha256(backup) == entry["sha256"] and backup.stat().st_size == entry["bytes"], f"V5 exact runtime backup changed: {path}")

    grid_checks = {}
    for color in COLORS:
        old, new = old_assets[color], new_assets[color]
        require(old["destination"] == new["destination"], f"Existing asset path changed: {color}")
        require(new.get("metadataUnchanged") is True and new.get("oppositeEdgePixelsEqual") is True, f"V6 importer/seam build failed: {color}")
        for path_key, hash_key in (("source", "sourceSha256"), ("destination", "runtimeSha256")):
            require(sha256(root / new[path_key]) == new[hash_key], f"Final art build hash changed: {new[path_key]}")
        require(new.get("width") == new.get("height") == RUNTIME_SIZE, f"V6 build dimensions are wrong: {color}")
        grid_checks[color] = verify_grid_pixels(root / new["destination"])
        require(backup_rows[old["destination"]]["sha256"] == old["runtimeSha256"], f"V5 backup differs from its build: {color}")
        for view in VIEWPORTS:
            a, b = before[(view, color)], after[(view, color)]
            require(a["textureGuid"] == b["textureGuid"] and a["texturePath"] == b["texturePath"] == new["destination"], f"Element binding changed: {view}/{color}")
            require(a["textureSha256"] == old["runtimeSha256"] and b["textureSha256"] == new["runtimeSha256"], f"Native rendering used another texture: {view}/{color}")
            require(b["textureWidth"] == b["textureHeight"] == RUNTIME_SIZE, f"Native runtime texture dimensions changed: {view}/{color}")
    require(reports["after"].get("sourceSet") == "Pattern-v6", "Native final run has another selected source set")
    verify_fixture_grid_report(reports["after"], new_assets, grid_checks, after)
    for key in ("pointerClicks", "navigationCycles", "homeReloads", "nativeTabCycles", "semanticChecks", "scrollingChecks", "aspectChecks"):
        number(reports["after"], key, positive=True)

    # Reuse original V5 reports only after validating the exact native bytes,
    # matching prior execution-source manifest, successful exits and code scope.
    runs = {run.get("name"): run for run in reports["prior_runs"].get("runs", [])}
    for name in ("geometry", "pointer", "compile"):
        key = "prior_" + name
        passed_report(reports[key], "Reused V5 " + name)
        require(reports[key].get("unityVersion") == reports["after"].get("unityVersion"), f"Reused {name} uses another Unity version")
        require(name in runs and runs[name].get("exitCode") == 0 and runs[name].get("sourceBaseHead") == BASE_HEAD,
                f"Reused V5 {name} runner receipt is missing or failed")
        require(runs[name]["nativeReportSha256"] == sha256(paths[key]) and runs[name]["sourceParityManifestSha256"] == V5_REPORT_HASHES["source-parity.json"],
                f"Reused V5 {name} is not the frozen execution/report version")
    require(reports["prior_compile"].get("check") == "compile" and reports["prior_compile"].get("activeBuildTarget") == "iOS", "Reused compile record is not an iOS script compilation")
    tracked = subprocess.run(["git", "diff", "--name-only", "-z", "HEAD"], cwd=root, check=True, capture_output=True, text=True).stdout.split("\0")
    allowed = {item["destination"] for item in new_assets.values()} | {"Assets/Scene/MainScene/MainMenuScene.unity"}
    require(not [path for path in tracked if path.startswith("Assets/") and path not in allowed], "Additional tracked runtime changes prevent prior validation reuse")
    require(not [path for path in tracked if path.startswith(("Packages/", "ProjectSettings/"))],
            "Package/project configuration changes prevent prior validation reuse")
    untracked = subprocess.run(["git", "ls-files", "--others", "--exclude-standard", "-z", "Assets"], cwd=root, check=True, capture_output=True, text=True).stdout.split("\0")
    require(not [path for path in untracked if path.endswith(".cs") and not path.startswith("Assets/Editor/") and path != "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs"], "Additional runtime C# source prevents prior compilation reuse")
    frames = native_frames(root, before_dir / "Runtime", reports["before"], "Before-v5")
    frames.update(native_frames(root, review / "After/Runtime", reports["after"], "After"))
    previous_receipt = reports["prior_pdf_receipt"]
    require(previous_receipt.get("sourceSet") == "Pattern-v5" and previous_receipt.get("sha256") == sha256(root / OLD_PDF),
            "Prior V5 PDF receipt does not identify the preserved previous deliverable")
    for (stage, view, color), frame in frames.items():
        if stage == "Before-v5":
            require(previous_receipt.get("screenshotHashes", {}).get(f"After/{view}/{color}") == sha256(frame),
                    f"Frozen V5 photo differs from the preceding final deliverable source: {view}/{color}")
    verify_capture_inventory(before_dir / "Runtime", reports["before"])
    verify_capture_inventory(review / "After/Runtime", reports["after"])
    density = [{"viewport": view, "color": color, "v5AreaEquivalent": sampled_area_equivalent(before[(view, color)], v6=False),
                "v6AreaEquivalent": sampled_area_equivalent(after[(view, color)], v6=True)} for view in VIEWPORTS for color in COLORS]
    for row in density:
        row["relativeReduction"] = 1 - row["v6AreaEquivalent"] / row["v5AreaEquivalent"]
        require(row["relativeReduction"] > 0, "V6 sampled grid is not sparser than V5")
    return {"root": root, "review": review, "paths": paths, "reports": reports, "before": before, "after": after,
            "frames": frames, "comparison": comparison, "gridChecks": grid_checks, "density": density,
            "productionEquality": {path: new_parity[path]["sha256"] for path in sorted(production)}}


class FourPageBuilder(PdfBuilder):
    def page(self, title: str, subtitle: str = "") -> None:
        from reportlab.lib.colors import HexColor
        if self.page_no:
            self.canvas.showPage()
        self.page_no += 1
        self.canvas.setFillColor(HexColor("#FAF8F3"))
        self.canvas.rect(0, 0, self.WIDTH, self.HEIGHT, fill=1, stroke=0)
        self.text("POCKETSTRIKER / SIMPLE GRID REVIEW", 34, 770, size=8, color="#647383")
        self.text(title, 34, 746, size=20)
        if subtitle:
            self.paragraph(subtitle, 34, 729, 544, size=9.5, bottom=686)
        self.canvas.setStrokeColor(HexColor("#D8D4CB"))
        self.canvas.line(34, 46, self.WIDTH - 34, 46)
        self.text("完整 Unity Editor 实景 | 本地验证，非真机认证", 34, 29, size=8, color="#6F7780")
        self.text(f"{self.page_no} / 4", self.WIDTH - 60, 29, size=8, color="#6F7780")

    def finish(self) -> None:
        require(self.page_no == 4, "This separate review must have four pages")
        self.canvas.save()


def build(evidence: dict, output: Path, font: Path, quality: int) -> None:
    b = FourPageBuilder(output, font, quality)
    b.canvas.setTitle("PocketStriker 主界面简疏同单元背景")
    for stage, title, subtitle in (
        ("Before-v5", "改前 V5 / 六色实际小屏", "冻结上一轮已通过的最终 V5 截图，原始字节保留；这轮没有重拍改前。"),
        ("After", "改后 V6 / 六色实际小屏", STYLE),
    ):
        b.page(title, subtitle)
        b.text("375x667 | 实际主界面六状态 | 五元素 + Null", 34, 683, size=9, color="#65717B")
        for n, color in enumerate(COLORS):
            x, y = 46 + (n % 3) * 181, (379 if n < 3 else 78)
            b.text(LABELS[color][0], x, y + 282, size=11, color=ACCENTS[color])
            b.image(evidence["frames"][(stage, "375x667", color)], x, y, 155, 155 * 667 / 375)
        b.text("Null 是真实无焦点回退；画面保留红色角色预览。", 34, 61, size=8.5, color="#65717B")
    b.page("红色 / 手机与平板对照", "同角色、同 idle=0 与模型根变换；保留原生镜头适配，差异量化见末页。")
    for stage, x in (("Before-v5", 112), ("After", 334)):
        b.text(f"{'改前 V5' if stage == 'Before-v5' else '改后 V6'} | 375x667 手机", x - 7, 700, size=10)
        b.image(evidence["frames"][(stage, "375x667", "red")], x, 403, 161, 161 * 667 / 375)
    for stage, x in (("Before-v5", 43), ("After", 327)):
        b.text(f"{'改前 V5' if stage == 'Before-v5' else '改后 V6'} | 768x1024 平板", x, 390, size=10)
        b.image(evidence["frames"][(stage, "768x1024", "red")], x, 62, 242, 242 * 1024 / 768)
    b.page("实际结果与验证范围", "新纹理与本轮场景结果读取真实材料；生产代码未变的旧验证单独说明。")
    r = evidence["reports"]["after"]
    red = next(item for item in evidence["density"] if item["viewport"] == "375x667" and item["color"] == "red")
    rows = [
        ("更疏的格子 (红色小屏)", f"背景采样面积等值约 {red['v5AreaEquivalent']:.2f} → {red['v6AreaEquivalent']:.2f} 格，减少 {red['relativeReduction'] * 100:.2f}%"),
        ("单元与循环边界", f"6 张 1536x1536 纹理；每色 6x6 格，256x256px 单元逐字节相同；{r['nativeEdgeChecks']} 项边界检查；{r['densityChecks']} 项视口密度检查"),
        ("本轮实际场景与导航", f"{len(r['cases'])} 场景 / {r['viewportCases']} 比例；{r['pointerClicks']} 原生点击；{r['navigationCycles']} 次返回；{r['homeReloads']} 次 HOME 重建"),
        ("本轮映射、滚动与比例", f"{r['semanticChecks']} 项语义检查；{r['scrollingChecks']} 项滚动；{r['aspectChecks']} 项比例；{r['nativeTabCycles']} 次页签循环"),
        ("本轮完整恢复检查", f"{r['preservedPhaseChecks']} 项相位保持；{r['resizeChecks']} 次尺寸切换；{r['zeroRectRecoveryChecks']} 项零矩形恢复；{r['lateTextureRecoveryChecks']} 项延迟纹理恢复"),
        ("生产控制与验证复用", "9 个场景、组件及导入配置文件 SHA 保持；V5 界面几何、原生交互和 iOS 脚本编译记录复用，本轮未重跑"),
    ]
    y = b.table(rows, 699, size=9) - 18
    b.text("数量口径与可恢复性", 34, y, size=11)
    y = b.paragraph(
        "面积等值 = 纹理像素面积 x 实际 UV 采样面积 / 单元间距面积。V5 源同排间距 156.5px、错排行距 78.25px；"
        "V6 为 256px 正交格。该值覆盖背景采样范围，不是被按钮/角色遮挡后仍完整可见的菱形枚数。"
        "旧 V5 六张 PNG、.meta、源图与原始报告均保留，资源 GUID 与切换/滚动语义保持。",
        34, y - 8, 544, size=9) - 15
    b.text("比较控制与实际限制", 34, y, size=11)
    c = evidence["comparison"]["summary"]
    y = b.paragraph(
        f"全部 24 对照使用同角色、idle=0 和模型根。原生镜头实时 bounds-fit，尺寸最大相对差 {c['cameraSizeRelativeDelta'] * 100:.3f}%，"
        f"位置最大分量差 {c['cameraPositionDelta']:.6f}。Null 无原生无属性角色，真实 PreScene.SetFocusingUnit(null) 保留红色预览。"
        "单元与边界相同性在 PNG 解码层验证，不代表 GPU 压缩/过滤后逐像素相同。"
        "Unity Editor / 本地 Addressables 离线验证，隔离账户、广告与 IAP；未验证真机触摸、安全区、GPU、线上加载或 IPA/TestFlight。"
        "HOME 重建为真实界面层重建。仅本地完成，无 push/publish。",
        34, y - 8, 544, size=8.8) - 10
    if evidence["reports"]["prior_geometry"].get("sceneDirtyOnlyFinding") is True:
        y = b.paragraph("复用的 V5 静态几何报告接纳隔离空场景 dirty 标记副作用；对象/组件指纹与预览场景数证实恢复。", 34, y, 544, size=8.5) - 10
    b.paragraph(
        f"源基线 HEAD: {BASE_HEAD}\n证据: Logs/MenuBackgroundSimplifyReview/Before-v5 与 After/Runtime；build.json；"
        "After/source-parity.json。旧验证来源: Logs/MenuBackgroundReview/Regression。V5 恢复: "
        "Tools/Art/build_menu_backgrounds.py --restore-pattern-v5。当前源版本不代表远程包 SHA。",
        34, y, 544, size=8, color="#65717B")
    b.finish()


def render_and_check(output: Path, render_dir: Path, dpi: int) -> dict:
    import pymupdf
    from pypdf import PdfReader
    reader = PdfReader(output)
    require(len(reader.pages) == 4, "Written simple-grid PDF has wrong page count")
    text = [page.extract_text() or "" for page in reader.pages]
    require(all(f"{n} / 4" in page for n, page in enumerate(text, 1)), "Page numbering/text extraction failed")
    require("PreScene.SetFocusingUnit(null)" in text[3] and "本轮未重跑" in text[3], "Scope/method disclosure is missing")
    embedded = any(
        ref.get_object().get("/FontDescriptor") and "/FontFile2" in ref.get_object()["/FontDescriptor"].get_object()
        for page in reader.pages for ref in page["/Resources"]["/Font"].get_object().values()
    )
    require(embedded, "Chinese TrueType font is not embedded")
    render_dir.mkdir(parents=True, exist_ok=True)
    renders = []
    with pymupdf.open(output) as document:
        for n, page in enumerate(document, 1):
            path = render_dir / f"page-{n:02d}.png"
            page.get_pixmap(matrix=pymupdf.Matrix(dpi / 72, dpi / 72), alpha=False).save(path)
            renders.append(str(path))
    return {"pages": 4, "embeddedChineseTrueType": True, "renderedPages": renders, "visualInspectionRequired": True}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path, default=ROOT / "output/pdf" / OUTPUT_NAME)
    parser.add_argument("--render-dir", type=Path, default=ROOT / "tmp/pdfs/menu-simple-grid")
    parser.add_argument("--font", type=Path, default=FONT_DEFAULT)
    parser.add_argument("--jpeg-quality", type=int, default=88)
    parser.add_argument("--render-dpi", type=int, default=120)
    parser.add_argument("--max-bytes", type=int, default=10_000_000)
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    root, output = args.root.resolve(), args.output.resolve()
    require(output.name == OUTPUT_NAME and output != (root / OLD_PDF).resolve(), "Use the separate simple-grid filename; previous Library PDF is protected")
    require(70 <= args.jpeg_quality <= 95 and 72 <= args.render_dpi <= 200, "Invalid JPEG quality/render DPI")
    old_path = root / OLD_PDF
    old_hash = sha256(old_path) if old_path.is_file() else None
    evidence = verify(root)
    if args.validate_only:
        print(json.dumps({"validated": True, "sourceSet": "Pattern-v6", "cases": len(evidence["after"]),
                          "productionFilesUnchanged": len(evidence["productionEquality"]),
                          "comparison": evidence["comparison"]["summary"], "density": evidence["density"]}, ensure_ascii=False, indent=2))
        return 0
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.stem + ".building.pdf")
    try:
        build(evidence, temporary, args.font, args.jpeg_quality)
        require(temporary.stat().st_size < args.max_bytes, "Simple-grid PDF exceeds the delivery byte limit")
        inspection = render_and_check(temporary, args.render_dir.resolve(), args.render_dpi)
        require(old_hash is None or sha256(old_path) == old_hash, "Previous Library PDF changed during this run")
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    receipt = {
        "path": str(output), "bytes": output.stat().st_size, "sha256": sha256(output), "utc": datetime.now(timezone.utc).isoformat(),
        "baseHead": BASE_HEAD, "beforeSourceSet": "Pattern-v5", "afterSourceSet": "Pattern-v6", "previousPdfUnchangedSha256": old_hash,
        "reports": {name: {"path": str(path), "sha256": sha256(path)} for name, path in evidence["paths"].items()},
        "screenshotHashes": {"/".join(key): sha256(path) for key, path in evidence["frames"].items()},
        "productionEquality": evidence["productionEquality"], "gridChecks": evidence["gridChecks"],
        "areaEquivalentMethod": {"v5SameRowPitchPixels": V5_ROW_PITCH_X, "v5StaggeredRowPitchPixels": V5_ROW_PITCH_Y, "v6OrthogonalUnitPixels": UNIT,
                                 "scope": "Background sampled-area equivalents, not complete uncovered native UI motif counts"},
        "density": evidence["density"], "comparison": evidence["comparison"], "jpegQuality": args.jpeg_quality,
        "priorValidationReused": ["V5 geometry", "V5 native UI pointer", "V5 iOS player-script compile"],
        "librarySaved": False, **inspection,
    }
    receipt_path = output.with_suffix(".receipt.json")
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: receipt[key] for key in ("path", "bytes", "sha256", "pages", "visualInspectionRequired")}, ensure_ascii=False, indent=2))
    print(f"Local receipt: {receipt_path}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Simple-grid review build stopped: {error}", file=sys.stderr)
        raise SystemExit(2)

#!/usr/bin/env python3
"""Build the eight-page, source-checked menu background review PDF.

This is a local artifact builder, not a Unity runner or Library uploader. Run the
PDF skill's artifact-operation marker immediately before the first authoring run,
then inspect every rendered page before delivery. ``--validate-only`` performs
read-only evidence checks and never authors a PDF.

Example (using the review's isolated Python environment)::

    python Tools/Art/build_menu_background_review_pdf.py --validate-only
    python Tools/Art/build_menu_background_review_pdf.py

All evidence comes from canonical ``Logs/MenuBackgroundReview`` copies. Missing,
unfinished, failing, stale, or mismatched reports stop the build before output.
The resulting receipt describes the local file only; it never claims a Library
file ID, a device build, a TestFlight result, or publication.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import subprocess
import sys
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]
BASE_HEAD = "5e4ff2bb3f725912018b7105ddd8b0ca67d40185"
COLORS = ("red", "green", "blue", "light", "dark", "null")
VIEWPORTS = ("375x667", "390x844", "540x960", "768x1024")
LABELS = {
    "red": ("赤 / Red", "暖灰红"),
    "green": ("绿 / Green", "灰绿"),
    "blue": ("蓝 / Blue", "灰蓝"),
    "light": ("光 / Light", "旧金"),
    "dark": ("暗 / Dark", "灰紫"),
    "null": ("无属性 / Null", "中性灰"),
}
ACCENTS = {
    "red": "#AB7465", "green": "#738B70", "blue": "#6A8492",
    "light": "#AA9670", "dark": "#8B7995", "null": "#929398",
}
DEFAULT_STYLE = (
    "低饱和的规则菱形底纹，点缀少量小像素技能石与星点。"
    "保留温和、素朴的老游戏感，让角色和白色界面保持清晰。"
)
FONT_DEFAULT = Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf")


class EvidenceError(RuntimeError):
    """The supplied evidence does not support a final deliverable."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise EvidenceError(message)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc_value(value: object, name: str) -> datetime:
    require(isinstance(value, str) and bool(value), f"{name}: missing UTC execution time")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    require(parsed.tzinfo is not None, f"{name}: execution time has no timezone")
    return parsed.astimezone(timezone.utc)


def load_json(path: Path) -> dict:
    require(path.is_file(), f"Required report is missing: {path}")
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), f"Report is not an object: {path}")
    return value


def has_failures(value: object) -> bool:
    if isinstance(value, list):
        return bool(value)
    if isinstance(value, (int, float)):
        return value != 0
    return bool(value)


def passed_report(data: dict, name: str, *, complete: bool = False) -> None:
    require(data.get("passed") is True, f"{name}: report has not passed")
    if complete:
        require(data.get("complete") is True, f"{name}: report is unfinished")
    for key in ("errors", "failures"):
        require(not has_failures(data.get(key, [])), f"{name}: {key} is nonempty")


def number(data: dict, key: str, *, positive: bool = False) -> int:
    value = data.get(key)
    require(isinstance(value, int) and not isinstance(value, bool), f"Missing integer: {key}")
    require(value >= (1 if positive else 0), f"Invalid count {key}: {value}")
    return value


def vector_delta(a: dict, b: dict) -> float:
    require(all(key in a and key in b for key in ("x", "y", "z")), "Missing vector fields")
    return max(abs(float(a[key]) - float(b[key])) for key in ("x", "y", "z"))


def case_index(report: dict, name: str) -> dict[tuple[str, str], dict]:
    cases = report.get("cases")
    require(isinstance(cases, list), f"{name}: missing cases")
    indexed = {(case["viewport"], case["color"]): case for case in cases}
    wanted = {(view, color) for view in VIEWPORTS for color in COLORS}
    require(len(cases) == len(indexed) == len(wanted), f"{name}: duplicate or incomplete cases")
    require(set(indexed) == wanted, f"{name}: must cover six colors at all four viewports")
    require(number(report, "viewportCases") == len(VIEWPORTS), f"{name}: incomplete viewport count")
    for (viewport, color), case in indexed.items():
        for key in ("nativeReturn", "nativeReload", "backgroundCoversViewport", "textureRepeat", "sourceRaycastDisabled"):
            require(case.get(key) is True, f"{name}/{viewport}/{color}: {key} failed")
        require(case.get("activeBackgrounds") == 1, f"{name}/{viewport}/{color}: wrong active background count")
        require(case.get("backgroundIndex") == COLORS.index(color), f"{name}/{viewport}/{color}: mapping changed")
        require(case.get("focusIsNull") == (color == "null"), f"{name}/{viewport}/{color}: Null semantics changed")
    return indexed


def compare_controls(before: dict, after: dict) -> dict:
    """Measure actual native-camera differences; do not assume exact equality."""
    rows = []
    for key in sorted(before):
        old, new = before[key], after[key]
        old_size, new_size = float(old["cameraSize"]), float(new["cameraSize"])
        require(old_size > 0 and math.isfinite(new_size), f"Invalid camera size: {key}")
        row = {
            "viewport": key[0], "color": key[1],
            "recordIdSame": old.get("recordId") == new.get("recordId"),
            "poseSame": old.get("poseHash") == new.get("poseHash")
            and old.get("poseNormalizedTime") == new.get("poseNormalizedTime") == 0,
            "rootPositionDelta": vector_delta(old["modelPosition"], new["modelPosition"]),
            "rootRotationDelta": vector_delta(old["modelRotation"], new["modelRotation"]),
            "cameraPositionDelta": vector_delta(old["cameraPosition"], new["cameraPosition"]),
            "cameraRotationDelta": vector_delta(old["cameraRotation"], new["cameraRotation"]),
            "cameraSizeRelativeDelta": abs(new_size - old_size) / old_size,
        }
        require(row["recordIdSame"] and row["poseSame"], f"Unmatched fighter or idle pose: {key}")
        require(row["rootPositionDelta"] <= 1e-5 and row["rootRotationDelta"] <= 1e-5,
                f"Unmatched model root transform: {key}")
        # Production camera bounds fitting can differ slightly between runs.
        # These values are printed and recorded instead of silently normalized.
        rows.append(row)
    numeric = ("rootPositionDelta", "rootRotationDelta", "cameraPositionDelta",
               "cameraRotationDelta", "cameraSizeRelativeDelta")
    summary = {key: max(row[key] for row in rows) for key in numeric}
    summary["allRecordsAndPosesSame"] = all(row["recordIdSame"] and row["poseSame"] for row in rows)
    return {"summary": summary, "cases": rows}


@dataclass
class Evidence:
    root: Path
    review: Path
    reports: dict
    paths: dict
    before_cases: dict
    after_cases: dict
    comparison: dict
    functional_name: str
    screenshots: dict
    preserved_metadata_count: int
    parity_count: int
    changed_paths: list[str]


def verify_evidence(root: Path) -> Evidence:
    from PIL import Image

    review = root / "Logs/MenuBackgroundReview"
    paths = {
        "workspace": review / "Before/workspace.json",
        "before": review / "Before/Runtime/report.json",
        "after": review / "After/Runtime/report.json",
        "build": review / "build.json",
        "parity": review / "After/source-parity.json",
        "geometry": review / "Regression/geometry-report.json",
        "pointer": review / "Regression/ui-pointer-report.json",
        "compile": review / "Regression/compile-iOS-report.json",
        "run_receipts": review / "Regression/run-receipts.json",
    }
    reports = {name: load_json(path) for name, path in paths.items()}
    workspace = reports["workspace"]
    require(workspace.get("head") == BASE_HEAD and workspace.get("startWasClean") is True,
            "The baseline must identify the checked clean local source head")
    current_head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, check=True,
                                  capture_output=True, text=True).stdout.strip()
    require(current_head == workspace["head"], "HEAD changed after this review baseline")
    for name in ("before", "after"):
        data = reports[name]
        passed_report(data, name, complete=True)
        require(data.get("externalServicesIsolated") is True and data.get("sourceAssetsUnchanged") is True,
                f"{name}: isolation or runtime source preservation failed")
        # Historical fixture records use the actual nine-character local HEAD;
        # a nonempty exact prefix is accepted only at that known recorded length.
        recorded_head = data.get("sourceHead")
        require(recorded_head in (BASE_HEAD, BASE_HEAD[:9]), f"{name}: wrong source head")
    require(reports["before"].get("baseline") is True, "Before is not a baseline run")
    require(reports["after"].get("baseline") is False, "After is not an acceptance run")
    before_cases = case_index(reports["before"], "Before")
    after_cases = case_index(reports["after"], "After")
    comparison = compare_controls(before_cases, after_cases)

    build = reports["build"]
    textures = build.get("textures", [])
    by_color = {("null" if entry.get("theme") == "neutral" else entry.get("theme")): entry for entry in textures}
    require(len(textures) == 6 and set(by_color) == set(COLORS), "Build must identify six final textures")
    require(isinstance(build.get("sourceSet"), str) and bool(build["sourceSet"]), "Missing selected source set")
    for color in COLORS:
        item = by_color[color]
        require(item.get("metadataUnchanged") is True and item.get("oppositeEdgePixelsEqual") is True,
                f"{color}: build metadata/seam validation failed")
        for path_key, hash_key in (("source", "sourceSha256"), ("destination", "runtimeSha256")):
            path = root / item[path_key]
            require(path.is_file() and sha256(path) == item[hash_key], f"Stale build hash: {path}")
        with Image.open(root / item["destination"]) as image:
            require(image.size == (item["width"], item["height"]), f"Stale build dimensions: {color}")
            require(image.crop((0, 0, 1, image.height)).tobytes()
                    == image.crop((image.width - 1, 0, image.width, image.height)).tobytes()
                    and image.crop((0, 0, image.width, 1)).tobytes()
                    == image.crop((0, image.height - 1, image.width, image.height)).tobytes(),
                    f"Actual runtime tile boundaries do not match: {color}")
        for view in VIEWPORTS:
            old, new = before_cases[(view, color)], after_cases[(view, color)]
            require(new["texturePath"] == item["destination"] and new["textureSha256"] == item["runtimeSha256"],
                    f"Final rendered texture is not the final build: {view}/{color}")
            require(new["textureWidth"] == item["width"] and new["textureHeight"] == item["height"],
                    f"Final rendered dimensions changed: {view}/{color}")
            require(old["textureGuid"] == new["textureGuid"] and old["texturePath"] == new["texturePath"],
                    f"Original six-color texture binding changed: {view}/{color}")
        meta = item["destination"] + ".meta"
        original = review / "Before/Source" / meta
        require(original.is_file() and sha256(original) == sha256(root / meta), f"Original importer/GUID changed: {meta}")
        original_image = review / "Before/Source" / item["destination"]
        require(original_image.is_file() and sha256(original_image) == before_cases[(VIEWPORTS[0], color)]["textureSha256"],
                f"Baseline backup does not match actual baseline rendering: {color}")

    parity = reports["parity"]
    require(parity.get("baseHead") == BASE_HEAD, "Parity report has the wrong base head")
    entries = parity.get("files", [])
    expected = {entry["destination"] for entry in textures} | {entry["destination"] + ".meta" for entry in textures}
    expected |= {
        "Assets/Scene/MainScene/MainMenuScene.unity",
        "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs",
        "Assets/MainSceneSystem/ScrollingBackgroundAspectFill.cs.meta",
        "Assets/Editor/PocketStrikerMenuBackgroundValidation.cs",
        "Assets/Editor/PocketStrikerMenuBackgroundValidation.cs.meta",
    }
    require(len(entries) == len(expected) == 17 and {entry["path"] for entry in entries} == expected,
            "Source parity must cover the exact 17 execution files")
    for entry in entries:
        require(entry.get("sameExecutionBytes") is True, f"Execution parity failed: {entry['path']}")
        require(sha256(root / entry["path"]) == entry["sha256"], f"Source changed after parity report: {entry['path']}")
        execution = Path(parity.get("executionProject", "")) / entry["path"]
        if execution.is_file():
            require(sha256(execution) == entry["sha256"], f"Execution copy changed: {entry['path']}")

    # The six mappings, focus call path and scrolling package retain their actual
    # pre-change bytes; no logical equality is inferred from screenshots alone.
    for relative in ("Assets/Prescene/PreScene.cs", "Packages/com.mcombat.shared/Runtime/UI/BackGroundPS.cs"):
        original = review / "Before/Source" / relative
        require(original.is_file() and sha256(original) == sha256(root / relative), f"Production semantics changed: {relative}")

    screenshots = {}
    for stage, cases in (("Before", before_cases), ("After", after_cases)):
        for (view, color), case in cases.items():
            path = review / stage / "Runtime" / view / f"{color}-home.png"
            require(path.is_file(), f"Missing canonical native screenshot: {path}")
            require(str(case["screenshot"]).endswith(f"/{view}/{color}-home.png"), f"Unexpected screenshot reference: {stage}/{view}/{color}")
            with Image.open(path) as image:
                require(image.size == tuple(int(value) for value in view.split("x")), f"Screenshot is not the requested actual backbuffer: {path}")
            execution = Path(case["screenshot"])
            if execution.is_file():
                require(sha256(execution) == sha256(path), f"Canonical screenshot differs from its native source: {path}")
            screenshots[(stage, view, color)] = path

    functional_name = "after"
    if reports["after"].get("artRefresh") is True:
        paths["functional"] = review / "Draft-v2/Runtime/report.json"
        paths["functional_parity"] = review / "Draft-v2/source-parity.json"
        reports["functional"] = load_json(paths["functional"])
        reports["functional_parity"] = load_json(paths["functional_parity"])
        passed_report(reports["functional"], "Retained production callback run", complete=True)
        functional_cases = case_index(reports["functional"], "Retained production callback run")
        old_parity = {item["path"]: item for item in reports["functional_parity"].get("files", [])}
        production = expected - {entry["destination"] for entry in textures}
        production -= {"Assets/Editor/PocketStrikerMenuBackgroundValidation.cs", "Assets/Editor/PocketStrikerMenuBackgroundValidation.cs.meta"}
        for relative in production:
            require(relative in old_parity and old_parity[relative]["sha256"] == sha256(root / relative),
                    f"Reused callback evidence has different production code/import settings: {relative}")
        for key, case in functional_cases.items():
            final = after_cases[key]
            require((case["textureWidth"], case["textureHeight"]) == (final["textureWidth"], final["textureHeight"]),
                    f"Reused callback evidence has a different texture aspect: {key}")
        functional_name = "functional"
    functional = reports[functional_name]
    for key in ("aspectChecks", "preservedPhaseChecks", "resizeChecks", "zeroRectRecoveryChecks", "lateTextureRecoveryChecks"):
        number(functional, key, positive=True)
    for key in ("pointerClicks", "navigationCycles", "homeReloads", "nativeTabCycles", "semanticChecks", "scrollingChecks", "aspectChecks"):
        number(reports["after"], key, positive=True)

    for name in ("geometry", "pointer", "compile"):
        passed_report(reports[name], f"Final regression/{name}")
        # CompilePlayer's production schema has no utcTime/complete fields;
        # check=compile, target=iOS and passed establish its actual result.
        if name != "compile":
            require(reports[name].get("utcTime"), f"Final regression/{name}: no execution timestamp")
        if "complete" in reports[name]:
            require(reports[name]["complete"] is True, f"Final regression/{name}: unfinished")
        require(reports[name].get("unityVersion") == reports["after"].get("unityVersion"),
                f"Final regression/{name}: Unity version differs from final runtime evidence")
        if name != "compile":
            require(utc_value(reports[name]["utcTime"], name) >= utc_value(reports["after"]["utcTime"], "After"),
                    f"Final regression/{name}: report predates the final After acceptance run")
    geometry = reports["geometry"]
    for key in ("layoutCases", "registeredLayers", "compositions", "stageCardCases"):
        number(geometry, key, positive=True)
    require(geometry.get("independentGeometryPassed") is True and geometry.get("fixtureObjectsRestored") is True,
            "Final geometry regression does not establish geometry and fixture restoration")
    pointer = reports["pointer"]
    for key in ("viewportCases", "settingsTabCases", "popupCycles", "nicknameCycles", "linkPromptCycles",
                "returnActions", "raycastChecks", "disabledChecks", "buttonStateCaptures"):
        number(pointer, key, positive=True)
    compile_report = reports["compile"]
    require(compile_report.get("check") == "compile" and compile_report.get("activeBuildTarget") == "iOS",
            "Final compile report is not an iOS player-script compile")
    require(compile_report.get("unityVersion") == reports["after"].get("unityVersion"),
            "Final regression compilation uses a different Unity version")
    require(compile_report.get("expectedUnityVersion") == reports["after"].get("unityVersion"),
            "Final regression compilation expects a different Unity version")
    # Keep native fixture JSON bytes intact. Runner receipts independently bind
    # observed execution times and successful exits to the actual report bytes
    # and the exact source parity manifest used for this final rendering run.
    runs = reports["run_receipts"].get("runs", [])
    require(isinstance(runs, list), "Regression run receipts must contain a runs list")
    by_name = {run.get("name"): run for run in runs}
    require(len(runs) == len(by_name) == 3 and set(by_name) == {"geometry", "pointer", "compile"},
            "Regression run receipts must cover exactly geometry, pointer and compile")
    final_time = utc_value(reports["after"]["utcTime"], "After")
    for name in ("geometry", "pointer", "compile"):
        run = by_name[name]
        require(run.get("exitCode") == 0, f"Final regression/{name}: runner did not exit successfully")
        require(run.get("sourceBaseHead") == BASE_HEAD, f"Final regression/{name}: receipt has the wrong HEAD")
        require(run.get("sourceParityManifestSha256") == sha256(paths["parity"]),
                f"Final regression/{name}: receipt identifies a different execution source manifest")
        require(run.get("nativeReportSha256") == sha256(paths[name]),
                f"Final regression/{name}: receipt does not identify the native report bytes")
        started = utc_value(run.get("startedUtc"), f"{name} started")
        finished = utc_value(run.get("finishedUtc"), f"{name} finished")
        require(started >= final_time and finished >= started,
                f"Final regression/{name}: runner times predate After or are inconsistent")
        require(isinstance(run.get("command"), list) and bool(run["command"]),
                f"Final regression/{name}: runner command is missing")

    # Limit untouched-runtime claims to actual current tracked changes.
    changed = subprocess.run(["git", "diff", "--name-only", "-z", "HEAD"], cwd=root, check=True,
                             capture_output=True, text=True).stdout.rstrip("\0").split("\0")
    changed = [path for path in changed if path]
    permitted_assets = {entry["destination"] for entry in textures} | {"Assets/Scene/MainScene/MainMenuScene.unity"}
    unexpected_assets = [path for path in changed if path.startswith("Assets/") and path not in permitted_assets]
    require(not unexpected_assets, "Unexpected tracked runtime changes: " + ", ".join(unexpected_assets))
    return Evidence(root, review, reports, paths, before_cases, after_cases, comparison,
                    functional_name, screenshots, 6, len(entries), changed)


class PdfBuilder:
    """Simple fixed eight-page layout, with explicit overflow detection."""

    WIDTH, HEIGHT = 612.0, 792.0
    MARGIN = 34.0
    FONT = "MenuReviewArialUnicode"

    def __init__(self, output: Path, font: Path, jpeg_quality: int) -> None:
        from reportlab.pdfbase import pdfmetrics
        from reportlab.pdfbase.ttfonts import TTFont
        from reportlab.pdfgen.canvas import Canvas
        require(font.is_file(), f"Chinese TrueType font is missing: {font}")
        pdfmetrics.registerFont(TTFont(self.FONT, str(font)))
        self.canvas = Canvas(str(output), pagesize=(self.WIDTH, self.HEIGHT), pageCompression=1)
        self.canvas.setTitle("PocketStriker 主界面六背景实景验收")
        self.canvas.setAuthor("PocketStriker local workspace review")
        self.quality = jpeg_quality
        self.page_no = 0
        self.image_cache = {}

    def text(self, text: str, x: float, y: float, *, size: float = 10, color: str = "#283647") -> None:
        from reportlab.lib.colors import HexColor
        self.canvas.setFillColor(HexColor(color))
        self.canvas.setFont(self.FONT, size)
        self.canvas.drawString(x, y, text)

    def paragraph(self, text: str, x: float, top: float, width: float, *, size: float = 10,
                  color: str = "#283647", bottom: float = 44) -> float:
        from reportlab.lib.colors import HexColor
        from reportlab.lib.styles import ParagraphStyle
        from reportlab.platypus import Paragraph
        style = ParagraphStyle("body", fontName=self.FONT, fontSize=size, leading=size * 1.4,
                               wordWrap="CJK", textColor=HexColor(color), spaceAfter=0)
        paragraph = Paragraph(escape(text).replace("\n", "<br/>"), style)
        _, height = paragraph.wrap(width, self.HEIGHT)
        require(top - height >= bottom, f"Text overflows page {self.page_no}: {text[:70]}")
        paragraph.drawOn(self.canvas, x, top - height)
        return top - height

    def page(self, title: str, subtitle: str = "") -> None:
        from reportlab.lib.colors import HexColor
        if self.page_no:
            self.canvas.showPage()
        self.page_no += 1
        self.canvas.setFillColor(HexColor("#FAF8F3"))
        self.canvas.rect(0, 0, self.WIDTH, self.HEIGHT, fill=1, stroke=0)
        self.text("POCKETSTRIKER / LOCAL REVIEW", self.MARGIN, 770, size=8, color="#647383")
        self.text(title, self.MARGIN, 746, size=20)
        if subtitle:
            self.paragraph(subtitle, self.MARGIN, 729, self.WIDTH - self.MARGIN * 2, size=9.5, bottom=686)
        self.canvas.setStrokeColor(HexColor("#D8D4CB"))
        self.canvas.line(self.MARGIN, 46, self.WIDTH - self.MARGIN, 46)
        self.text("实际 Unity Editor 截图 | 本地验证，非真机认证", self.MARGIN, 29, size=8, color="#6F7780")
        self.text(f"{self.page_no} / 8", self.WIDTH - 60, 29, size=8, color="#6F7780")

    def image(self, path: Path, x: float, y: float, width: float, height: float) -> None:
        from PIL import Image
        from reportlab.lib.utils import ImageReader
        from reportlab.lib.colors import HexColor
        if path not in self.image_cache:
            with Image.open(path) as image:
                jpeg = io.BytesIO()
                # The native frame is kept whole and at its original pixel size.
                # JPEG embedding only reduces delivery bytes; no repaint/crop.
                image.convert("RGB").save(jpeg, format="JPEG", quality=self.quality,
                                          optimize=True, subsampling=0)
            jpeg.seek(0)
            self.image_cache[path] = ImageReader(jpeg)
        self.canvas.setFillColor(HexColor("#E5E1D8"))
        self.canvas.roundRect(x - 2, y - 2, width + 4, height + 4, 3, fill=1, stroke=0)
        self.canvas.drawImage(self.image_cache[path], x, y, width=width, height=height,
                              preserveAspectRatio=True, anchor="c", mask="auto")

    def table(self, rows: list[tuple[str, str]], top: float, *, widths=(180.0, 364.0), size=9.0) -> float:
        from reportlab.lib.colors import HexColor
        from reportlab.lib.styles import ParagraphStyle
        from reportlab.platypus import Paragraph, Table, TableStyle
        style = ParagraphStyle("cell", fontName=self.FONT, fontSize=size, leading=size * 1.35,
                               wordWrap="CJK", textColor=HexColor("#283647"))
        cells = [[Paragraph(escape(a), style), Paragraph(escape(b), style)] for a, b in rows]
        table = Table(cells, colWidths=widths, hAlign="LEFT")
        table.setStyle(TableStyle([
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("BACKGROUND", (0, 0), (0, -1), HexColor("#ECE9E1")),
            ("BACKGROUND", (1, 0), (1, -1), HexColor("#F3F0E9")),
            ("LINEBELOW", (0, 0), (-1, -1), 0.35, HexColor("#D8D4CB")),
            ("LEFTPADDING", (0, 0), (-1, -1), 8), ("RIGHTPADDING", (0, 0), (-1, -1), 8),
            ("TOPPADDING", (0, 0), (-1, -1), 5), ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ]))
        _, height = table.wrap(sum(widths), self.HEIGHT)
        require(top - height >= 50, f"Table overflows page {self.page_no}")
        table.drawOn(self.canvas, self.MARGIN, top - height)
        return top - height

    def finish(self) -> None:
        require(self.page_no == 8, "The compact review must have eight pages")
        self.canvas.save()


def build_pdf(evidence: Evidence, output: Path, font: Path, style_description: str,
              jpeg_quality: int) -> None:
    report = evidence.reports["after"]
    source_set = evidence.reports["build"]["sourceSet"]
    builder = PdfBuilder(output, font, jpeg_quality)
    builder.page("主界面六背景 / 最终实景", style_description)
    builder.text(f"选用 {source_set} | 375x667 小屏 | 五元素 + Null", 34, 683, size=9, color="#65717B")
    for index, color in enumerate(COLORS):
        column, row = index % 3, index // 3
        x = 46 + column * 181
        y = 379 if row == 0 else 78
        builder.text(LABELS[color][0], x, y + 282, size=11, color=ACCENTS[color])
        builder.image(evidence.screenshots[("After", "375x667", color)], x, y, 155, 155 * 667 / 375)
    builder.text("Null 为真实无焦点回退；沿用红色角色预览。对照和限制见后页。", 34, 61, size=8.5, color="#65717B")

    for color in COLORS:
        builder.page(f"{LABELS[color][0]} / 前后对照",
                     "同角色与 idle=0；完整原生截图。模型根一致，动态镜头差异见第 8 页。")
        # Two native ratios per color, with whole-screen framing preserved.
        for stage, x in (("Before", 112), ("After", 334)):
            builder.text(f"{'改前' if stage == 'Before' else '改后'} | 375x667 手机", x - 7, 700, size=10)
            builder.image(evidence.screenshots[(stage, "375x667", color)], x, 403, 161, 161 * 667 / 375)
        for stage, x in (("Before", 43), ("After", 327)):
            builder.text(f"{'改前' if stage == 'Before' else '改后'} | 768x1024 平板", x, 390, size=10)
            builder.image(evidence.screenshots[(stage, "768x1024", color)], x, 62, 242, 242 * 1024 / 768)

    builder.page("验证、改动与实际限制", "数字均读取本轮原始 JSON；截图、运行代码和纹理哈希已逐项核对。")
    final_rows = [
        ("最终渲染与导航", f"{len(report['cases'])} 色彩/视口场景；{report['pointerClicks']} 原生指针点击；" + ", ".join(VIEWPORTS)),
        ("返回、HOME 与元素语义", f"{report['navigationCycles']} 次返回；{report['homeReloads']} 次 HOME 重建；{report['nativeTabCycles']} 次原生页签循环；{report['semanticChecks']} 项语义检查"),
        ("最终纹理与比例", f"{report['scrollingChecks']} 项滚动检查；{report['aspectChecks']} 项比例检查；6 张纹理双向边界完全相等；6 份 GUID/importer 元数据保留"),
    ]
    functional = evidence.reports[evidence.functional_name]
    reuse = evidence.functional_name != "after"
    final_rows.append(("比例与滚动恢复" + (" (同代码留存证据)" if reuse else " (最终全量运行)"),
                       f"{functional['preservedPhaseChecks']} 项相位保持；{functional['resizeChecks']} 项反向缩放；"
                       f"{functional['zeroRectRecoveryChecks']} 项零矩形恢复；{functional['lateTextureRecoveryChecks']} 项延迟纹理恢复"))
    geometry, pointer, compile_report = (evidence.reports[name] for name in ("geometry", "pointer", "compile"))
    final_rows.extend([
        ("界面几何回归", f"{geometry['registeredLayers']} 个注册层；{geometry['layoutCases']} 项布局；{geometry['compositions']} 项组合；{geometry['stageCardCases']} 项关卡卡片"),
        ("原生交互回归", f"{pointer['viewportCases']} 比例；{pointer['settingsTabCases']} 项设置页签；{pointer['raycastChecks']} 次 raycast；{pointer['disabledChecks']} 项禁用门控；{pointer['buttonStateCaptures']} 张状态截图"),
        ("iOS 脚本编译 / 源码核对", f"Unity {compile_report['unityVersion']}，{compile_report['activeBuildTarget']} {compile_report['check']} 通过；{evidence.parity_count} 文件与执行副本哈希一致"),
    ])
    y = builder.table(final_rows, 699, size=8.9) - 17
    builder.text("实际改动与可恢复性", 34, y, size=11)
    finishing_modes = sorted({item.get("tileFinishingMode", "未记录处理模式")
                              for item in evidence.reports["build"]["textures"]})
    finishing_names = {
        "periodic-crop-and-edge-match": "检测近似重复周期并裁切、12 像素边界匹配",
        "calm-edge-feather": "平静边界渐变与边缘匹配",
        "legacy-middle-wrap": "旧版中部内容交叉拼接",
    }
    finishing = "、".join(finishing_names.get(mode, mode) for mode in finishing_modes)
    dimensions = sorted({f"{item['width']}x{item['height']}" for item in evidence.reports["build"]["textures"]})
    y = builder.paragraph(
        f"{source_set} 六色资源，实际运行纹理 {', '.join(dimensions)}；{finishing}。旧 PNG、.meta 和原始源图均保留。"
        "现有绑定、UI 布局、滚动速度/方向与 Null/Off/Next 语义保留。"
        "新增背景 UV 按比例裁切组件，保留既有滚动相位。战斗与登录画面代码/资源未修改。",
        34, y - 8, 544, size=9) - 14
    builder.text("比较控制与验证范围", 34, y, size=11)
    comparison = evidence.comparison["summary"]
    y = builder.paragraph(
        f"全部 {len(report['cases'])} 对照为同角色、静止姿态与模型根。原生镜头实时 bounds-fit，"
        f"相机尺寸最大相对差 {comparison['cameraSizeRelativeDelta'] * 100:.3f}%，"
        f"位置最大分量差 {comparison['cameraPositionDelta']:.6f}；未重画或对齐截图。"
        "Null 无原生无属性角色，真实 SetFocusingUnit(null) 保留红色角色预览。",
        34, y - 8, 544, size=9) - 10
    if reuse:
        y = builder.paragraph(
            "最终 artRefresh 运行重新验证画面、导航与纹理；恢复回调数字来自 Draft-v2 留存全量运行。"
            "生产场景、比例组件、元数据及纹理尺寸逐项一致后才复用该证据。",
            34, y, 544, size=8.8) - 10
    if geometry.get("sceneDirtyOnlyFinding") is True:
        y = builder.paragraph(
            f"静态布局底层报告有 {geometry.get('sourceLayoutErrors', 0)} 项隔离空场景 dirty 标记。"
            "上层通过对象/组件指纹与预览场景数证实恢复，仅接纳该标记副作用。",
            34, y, 544, size=8.8) - 10
    y = builder.paragraph(
        "本地 Addressables Fast Mode / Unity Editor 离线验证，隔离登录、账户更新、广告与 IAP。"
        "未验证真实设备触摸、安全区、GPU 表现、线上账户/动态加载或 IPA/TestFlight。"
        "HOME 重建为真实界面层重建，不等同于线上启动加载认证。仅本地完成，无 push/publish。",
        34, y, 544, size=8.8) - 10
    y = builder.paragraph(
        f"基线 HEAD: {BASE_HEAD}\n"
        "证据: Logs/MenuBackgroundReview/{Before,After}/Runtime/report.json；"
        "build.json；After/source-parity.json；Regression/*.json。"
        "旧资源恢复: Tools/Art/build_menu_backgrounds.py --restore-legacy。"
        "源版本与当前工作区已核对，不代表任何远程包 SHA。",
        34, y, 544, size=8, color="#65717B")
    builder.finish()


def inspect_and_render(output: Path, render_dir: Path, *, dpi: int) -> dict:
    import fitz
    from pypdf import PdfReader
    reader = PdfReader(str(output))
    require(len(reader.pages) == 8, "Written PDF has an unexpected page count")
    extracted = []
    embedded_true_type = False
    for page in reader.pages:
        extracted.append(page.extract_text() or "")
        resources = page.get("/Resources", {}).get_object()
        fonts = resources.get("/Font", {}).get_object()
        for font_ref in fonts.values():
            font = font_ref.get_object()
            descriptor = font.get("/FontDescriptor")
            if descriptor and "/FontFile2" in descriptor.get_object():
                embedded_true_type = True
    require(embedded_true_type, "Chinese TrueType font was not embedded")
    require(all(text.strip() for text in extracted), "Written PDF contains an empty text page")
    require("无属性" in extracted[6] and "iOS" in extracted[7], "Written PDF text verification failed")
    render_dir.mkdir(parents=True, exist_ok=True)
    rendered = []
    with fitz.open(output) as document:
        for index, page in enumerate(document):
            path = render_dir / f"page-{index + 1:02d}.png"
            page.get_pixmap(matrix=fitz.Matrix(dpi / 72, dpi / 72), alpha=False).save(path)
            rendered.append(str(path.resolve()))
    return {"pages": len(reader.pages), "embeddedChineseTrueType": embedded_true_type,
            "renderedPages": rendered, "visualInspectionRequired": True}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path, default=ROOT / "output/pdf/PocketStriker-Menu-Backgrounds-20261003.pdf")
    parser.add_argument("--render-dir", type=Path, default=ROOT / "tmp/pdfs/menu-review")
    parser.add_argument("--font", type=Path, default=FONT_DEFAULT)
    parser.add_argument("--style-description", default=DEFAULT_STYLE)
    parser.add_argument("--jpeg-quality", type=int, default=88)
    parser.add_argument("--render-dpi", type=int, default=120)
    parser.add_argument("--max-bytes", type=int, default=10_000_000)
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    require(70 <= args.jpeg_quality <= 95, "Use JPEG quality between 70 and 95")
    require(72 <= args.render_dpi <= 200, "Use render DPI between 72 and 200")
    evidence = verify_evidence(args.root.resolve())
    if args.validate_only:
        print(json.dumps({"validated": True, "sourceSet": evidence.reports["build"]["sourceSet"],
                          "cases": len(evidence.after_cases), "parityFiles": evidence.parity_count,
                          "functionalEvidence": evidence.functional_name,
                          "comparison": evidence.comparison["summary"]}, ensure_ascii=False, indent=2))
        return 0
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    # A temporary sibling keeps an existing delivered PDF recoverable on failure.
    temporary = output.with_name(output.stem + ".building.pdf")
    try:
        build_pdf(evidence, temporary, args.font, args.style_description, args.jpeg_quality)
        require(temporary.stat().st_size < args.max_bytes,
                f"PDF exceeds the delivery limit: {temporary.stat().st_size} >= {args.max_bytes} bytes")
        inspection = inspect_and_render(temporary, args.render_dir.resolve(), dpi=args.render_dpi)
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    receipt = {
        "path": str(output), "bytes": output.stat().st_size, "sha256": sha256(output),
        "utc": datetime.now(timezone.utc).isoformat(), "baseHead": BASE_HEAD,
        "sourceSet": evidence.reports["build"]["sourceSet"], "styleDescription": args.style_description,
        "jpegQuality": args.jpeg_quality, "maxBytes": args.max_bytes,
        "reports": {name: {"path": str(path), "sha256": sha256(path)} for name, path in evidence.paths.items()},
        "screenshotHashes": {"/".join(key): sha256(path) for key, path in evidence.screenshots.items()},
        "comparison": evidence.comparison, "parityFiles": evidence.parity_count,
        "preservedTextureMetadata": evidence.preserved_metadata_count,
        "functionalEvidence": evidence.functional_name, "trackedChangedPaths": evidence.changed_paths,
        "librarySaved": False, **inspection,
    }
    receipt_path = output.with_suffix(".receipt.json")
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: receipt[key] for key in ("path", "bytes", "sha256", "pages", "sourceSet", "visualInspectionRequired")}, ensure_ascii=False, indent=2))
    print(f"Local receipt: {receipt_path}")
    print(f"Inspect all rendered pages: {args.render_dir.resolve()}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Menu review build stopped: {error}", file=sys.stderr)
        raise SystemExit(2)

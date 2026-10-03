#!/usr/bin/env python3
"""Assemble native Unity captures at their recorded real-time cadence.

This changes no game assets, UVs or frame positions. The two viewports were
recorded separately; the comparison aligns their relative elapsed time.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
VIEWS = ("375x667", "768x1024")


def require(test, message):
    if not test:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(report_path, output):
    report = json.loads(report_path.read_text())
    require(report.get("passed") and report.get("complete")
            and report.get("previewOnly") and report.get("dynamicReview"),
            "A completed, new native dynamic preview is required")
    require(report.get("sourceSet") == "Pattern-v7", "Use the approved v3 source set")
    sequences = {item["viewport"]: item for item in report["dynamicSequences"]}
    require(set(sequences) == set(VIEWS), "Both native viewports are required")
    frames = {}
    evidence = []
    for view in VIEWS:
        sequence = sequences[view]
        require(sequence["color"] == "red" and sequence["realtimeElapsed"] >= 7
                and sequence["uvDeltaError"] < .0001,
                "Actual authored scrolling must pass for " + view)
        samples = sequence["samples"]
        require(len(samples) >= 20, "Too few actual frames for " + view)
        times = [sample["realtimeElapsed"] for sample in samples]
        require(all(b > a for a, b in zip(times, times[1:])), "Frame times must increase")
        loaded = []
        for sample in samples:
            path = report_path.parent / view / "dynamic-red" / Path(sample["screenshot"]).name
            require(path.is_file(), "Missing native capture: " + str(path))
            original = Path(sample["screenshot"])
            if original.is_file():
                require(digest(path) == digest(original), "Capture copy differs from Unity original")
            with Image.open(path) as image:
                require(image.size == tuple(map(int, view.split("x"))), "Native frame size mismatch")
                loaded.append(image.convert("RGB"))
            evidence.append({"viewport": view, "index": sample["index"],
                             "path": str(path.relative_to(ROOT)), "sha256": digest(path),
                             "requestUtc": sample["utcTime"],
                             "requestRelativeRealtime": sample["realtimeElapsed"],
                             "requestUv": sample["uvRect"]})
        frames[view] = (samples, loaded)

    # Phone cadence defines the output clock; tablet frames are the closest
    # actual recorded time, rather than synthesized or moved texture pixels.
    primary = sequences[VIEWS[0]]["samples"]
    height = 600
    sizes = [(round(height * int(v.split("x")[0]) / int(v.split("x")[1])), height) for v in VIEWS]
    gap, margin = 18, 20
    width = sum(size[0] for size in sizes) + gap + margin * 2
    canvas_size = (width, height + 100)
    font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 17)
    small_font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
    canvases = []
    selections = []
    for sample in primary:
        elapsed = sample["realtimeElapsed"]
        canvas = Image.new("RGB", canvas_size, "#1e242d")
        draw = ImageDraw.Draw(canvas)
        draw.text((margin, 13), "Approved v3 - actual Unity menu / authored scrolling speed", font=font, fill="#f1eee8")
        draw.text((margin, 38), f"t = {elapsed:.2f}s   |   Separate recordings aligned by relative time", font=small_font, fill="#c5c9d0")
        x = margin
        selection = {"outputRelativeRealtime": elapsed, "views": {}}
        for view, size in zip(VIEWS, sizes):
            samples, images = frames[view]
            index = min(range(len(samples)), key=lambda i: abs(samples[i]["realtimeElapsed"] - elapsed))
            canvas.paste(images[index].resize(size, Image.Resampling.LANCZOS), (x, 65))
            draw.text((x, height + 71), view + " / native capture", font=small_font, fill="#c5c9d0")
            selection["views"][view] = {"index": samples[index]["index"],
                                       "recordedRelativeRealtime": samples[index]["realtimeElapsed"]}
            x += size[0] + gap
        canvases.append(canvas)
        selections.append(selection)
    # GIF clocks have 10ms precision. Round cumulative times first, rather
    # than allowing the encoder to truncate every recorded frame interval.
    gif_clock = [round(sample["realtimeElapsed"] * 100) * 10 for sample in primary]
    durations = [max(10, b - a) for a, b in zip(gif_clock, gif_clock[1:])]
    durations.append(200)
    # A common palette prevents frame-to-frame palette flicker. Frames are
    # full menu captures; only thumbnail scaling and comparison labels change.
    swatch = Image.new("RGB", (canvas_size[0], canvas_size[1] * 3))
    for i, position in enumerate((0, len(canvases) // 2, len(canvases) - 1)):
        swatch.paste(canvases[position], (0, canvas_size[1] * i))
    palette = swatch.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in canvases]
    output.parent.mkdir(parents=True, exist_ok=True)
    indexed[0].save(output, save_all=True, append_images=indexed[1:], duration=durations,
                    loop=0, optimize=False, disposal=1)
    require(output.stat().st_size < 10_000_000, "Preview exceeds the 10MB delivery limit")
    record = {"status": "passed", "report": str(report_path.relative_to(ROOT)),
              "reportSha256": digest(report_path), "sourceSet": "Pattern-v7",
              "approvedSourceSha256": "d52cc22932de74214244f71b01dc24bc882853b5a98f8250226f2fa2e0fcf89d",
              "output": str(output.relative_to(ROOT)), "outputSha256": digest(output),
              "bytes": output.stat().st_size, "frames": len(indexed),
              "viewports": list(VIEWS), "outputSize": list(canvas_size),
              "durationsMs": durations, "selections": selections, "sourceFrames": evidence,
              "limitation": "Actual separate Editor recordings aligned by relative realtime. ScreenCapture UV clocks are recorded at request time. Full frames are scaled for this comparison. The GIF repeats its recorded segment, so the playback-loop jump is not a game texture seam. This is not a device recording."}
    output.with_suffix(".evidence.json").write_text(json.dumps(record, indent=2) + "\n")
    return {key: record[key] for key in ("status", "bytes", "frames", "viewports", "outputSize", "outputSha256")}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=ROOT / "output/artproposals/PocketStriker-Menu-Color-Blocks-Scroll-20261003.gif")
    args = parser.parse_args()
    print(json.dumps(build(args.report.resolve(), args.output.resolve()), indent=2))

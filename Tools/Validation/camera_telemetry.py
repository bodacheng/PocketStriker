#!/usr/bin/env python3
"""Measure camera motion and corner safety in BattleCameraSmoke transition reports.

Compare matching controlled segments after settling; natural combat includes
intentional tracking and cannot isolate jitter by itself. This utility never
starts Unity or modifies report files.
"""

import argparse
import json
import math
from pathlib import Path


def vector(frame, key):
    value = frame[key]
    return tuple(float(value[axis]) for axis in ("x", "y", "z"))


def metrics(transition, warmup):
    all_frames = transition.get("frames", [])
    frames = [frame for frame in all_frames if frame["elapsed"] >= warmup]
    result = {
        "name": transition["name"],
        "sampledFrames": len(frames),
        "clippedCorners": sum(frame.get("clippedCorners", 0) for frame in all_frames),
    }
    if len(frames) < 3:
        return result
    velocities = []
    travel = 0.0
    zoom_travel = 0.0
    for first, second in zip(frames, frames[1:]):
        dt = second["elapsed"] - first["elapsed"]
        if dt <= 0:
            raise ValueError(f"{transition['name']}: non-increasing elapsed time")
        a, b = vector(first, "cameraPosition"), vector(second, "cameraPosition")
        travel += math.dist(a, b)
        zoom_travel += abs(second["distance"] - first["distance"])
        velocities.append(tuple((y - x) / dt for x, y in zip(a, b)))
    seconds = frames[-1]["elapsed"] - frames[0]["elapsed"]
    distances = [frame["distance"] for frame in frames]
    result.update({
        "seconds": seconds,
        "framesPerSecond": (len(frames) - 1) / seconds,
        "cameraTravelPerSecond": travel / seconds,
        "cameraVelocityVariationPerSecond": sum(
            math.dist(a, b) for a, b in zip(velocities, velocities[1:])) / seconds,
        "zoomTravelPerSecond": zoom_travel / seconds,
        "zoomRange": max(distances) - min(distances),
    })
    return result


def read_report(path, warmup):
    report = json.loads(Path(path).read_text())
    transitions = report.get("transitions", [])
    if not transitions:
        raise ValueError(f"{path}: no transition telemetry")
    return {item["name"]: metrics(item, warmup) for item in transitions}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", help="Current BattleCameraSmoke report.json")
    parser.add_argument("--baseline", help="Earlier report.json with matching scenarios")
    parser.add_argument("--warmup", type=float, default=2.0,
                        help="Exclude this many opening seconds (default: 2)")
    parser.add_argument("--json", action="store_true", help="Print machine-readable comparison")
    args = parser.parse_args()
    if args.warmup < 0:
        parser.error("--warmup must be nonnegative")
    current = read_report(args.report, args.warmup)
    baseline = read_report(args.baseline, args.warmup) if args.baseline else {}
    rows = []
    for name, item in current.items():
        row = dict(item)
        earlier = baseline.get(name, {})
        for metric in ("cameraTravelPerSecond", "cameraVelocityVariationPerSecond", "zoomTravelPerSecond"):
            if metric in item and earlier.get(metric, 0) > 1e-8:
                row[metric + "Ratio"] = item[metric] / earlier[metric]
        rows.append(row)
    if args.json:
        print(json.dumps({"warmupSeconds": args.warmup, "cases": rows}, indent=2))
    else:
        print("Scenario | FPS | Camera path/s | Velocity variation/s | Zoom path/s | Clipped | Path ratio")
        print("--- | ---: | ---: | ---: | ---: | ---: | ---:")
        for row in rows:
            if "seconds" not in row:
                print(f"{row['name']} | insufficient settled frames | | | | {row['clippedCorners']} |")
                continue
            ratio = row.get("cameraTravelPerSecondRatio")
            ratio_text = f"{ratio:.3f}" if ratio is not None else ""
            print(f"{row['name']} | {row['framesPerSecond']:.1f} | {row['cameraTravelPerSecond']:.4f} | "
                  f"{row['cameraVelocityVariationPerSecond']:.4f} | {row['zoomTravelPerSecond']:.4f} | "
                  f"{row['clippedCorners']} | {ratio_text}")
    return 1 if any(row["clippedCorners"] for row in rows) else 0


if __name__ == "__main__":
    raise SystemExit(main())

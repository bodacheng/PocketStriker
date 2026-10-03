"""Build seamless, looping menu backgrounds from the generated artwork.

Run with the bundled Python runtime documented by Codex workspace dependencies.
The six source PNGs live outside Unity's Assets directory so only the finished
textures are imported into the game.
"""

import argparse
import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Tools/ArtSources/MenuBackgrounds"
LEGACY = SOURCE / "Legacy-5e4ff2"
PATTERN_V5_BACKUP = SOURCE / "Pattern-v5/runtime-backup"
PATTERN_V6_BACKUP = SOURCE / "Pattern-v6/runtime-backup"
PATTERN_V6_SNAPSHOT = ROOT / "Logs/MenuColorBlockReview/Before-v6"
DESTINATION = ROOT / "Assets/OrganizedResources/InUse/ExternalAssets/bg/bg_main"
UNIT_SIZE = 256
GRID_UNIT_SIZE = (260, 262)
GRID_SOURCE_CROP = {"x": 350, "y": 580, "width": 260, "height": 262}

BACKGROUNDS = {
    "red": "haku1034_neon8bits2dpixelatedsimpleblackdark_redwarmgamecartoon_b2820319-6293-4895-98f5-ccb12908afc7.png",
    "green": "haku1034_ancient_leaf8bits2dpixelatedsimpleblackdark_greencolda_55806a54-6561-4472-96b9-bce79487ea03.png",
    "blue": "haku1034_ancient_pattern8bits2dpixelatedsimpleblackcoldabstract_62782879-5a4b-4676-bdcd-4b40d2ff98b9.png",
    "light": "haku1034_ancient_pattern8bits2dpixelatedsimpleblackabstractflat_0378dac9-3b6d-4058-b449-3866dcab9c0e.png",
    "dark": "haku1034_neon8bits2dpixelatedsimpleblack_and_whiteabstractflat_374dc487-6147-4697-bff4-9c1b3e821f70.png",
    "neutral": "haku1034_lighting_skyin_the_style_of_2d_game_art2dsimplecartoon_04dc2ebb-8c63-4475-a089-21d04afd9ae3 (1).png",
}


def smoothstep(value: np.ndarray) -> np.ndarray:
    value = np.clip(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def crossfade_edges(image: np.ndarray, axis: int, fraction: float) -> np.ndarray:
    """Borrow image content from its middle to wrap each edge continuously."""
    extent = image.shape[axis]
    band = max(2, int(round(extent * fraction)))
    gradient = np.abs(
        np.roll(image, -1, axis=axis) - np.roll(image, 1, axis=axis)
    )
    averaged_axes = (0, 2) if axis == 1 else (1, 2)
    # Penalize both general busyness and isolated bright lines at the seam.
    column_or_row_detail = gradient.mean(axis=averaged_axes)
    column_or_row_detail += 0.2 * np.quantile(
        gradient, 0.99, axis=averaged_axes
    )
    smoothed_detail = np.convolve(
        column_or_row_detail, np.ones(21) / 21, mode="same"
    )
    low, high = int(extent * 0.2), int(extent * 0.8)
    calm_coordinate = low + int(np.argmin(smoothed_detail[low:high]))
    coordinate = np.arange(extent)
    interior_weight = smoothstep(np.minimum(coordinate, extent - 1 - coordinate) / band)
    shape = (1, extent, 1) if axis == 1 else (extent, 1, 1)
    interior_weight = interior_weight.reshape(shape)
    shifted = np.roll(image, extent - calm_coordinate, axis=axis)
    return image * interior_weight + shifted * (1.0 - interior_weight)


def equalize_edge_pixels(image: np.ndarray, axis: int, band: int) -> None:
    """Make opposite edge pixels identical with a small, smooth local correction."""
    extent = image.shape[axis]
    band = min(band, extent // 2)
    difference = (
        image[:, -1, :] - image[:, 0, :]
        if axis == 1
        else image[-1, :, :] - image[0, :, :]
    )
    strength = (1.0 - smoothstep(np.arange(band) / (band - 1))) * 0.5
    for inset, weight in enumerate(strength):
        if axis == 1:
            image[:, inset, :] += difference * weight
            image[:, extent - 1 - inset, :] -= difference * weight
        else:
            image[inset, :, :] += difference * weight
            image[extent - 1 - inset, :, :] -= difference * weight


def finish_calm_edges(image: np.ndarray, fraction: float = 0.08) -> np.ndarray:
    """Feather only the tile boundary to the source's calm corner paper color.

    Unlike the legacy middle-content wrap, this keeps side impact motifs from
    reappearing across the quiet top/bottom UI areas at the initial scroll phase.
    """
    height, width = image.shape[:2]
    corner_height, corner_width = max(2, int(height * 0.04)), max(2, int(width * 0.04))
    corners = np.concatenate([
        image[:corner_height, :corner_width].reshape(-1, 3),
        image[:corner_height, -corner_width:].reshape(-1, 3),
        image[-corner_height:, :corner_width].reshape(-1, 3),
        image[-corner_height:, -corner_width:].reshape(-1, 3),
    ])
    paper = np.median(corners, axis=0)
    x = np.arange(width)
    y = np.arange(height)
    horizontal = smoothstep(np.minimum(x, width - 1 - x) / max(2, width * fraction))
    vertical = smoothstep(np.minimum(y, height - 1 - y) / max(2, height * fraction))
    interior = (vertical[:, None] * horizontal[None, :])[:, :, None]
    return image * interior + paper * (1.0 - interior)


def periodic_crop(image: np.ndarray) -> tuple[np.ndarray, dict]:
    """Trim a regular generated pattern to complete repetition cycles.

    Autocorrelation uses the actual source pixels, so different generated tile
    sizes retain their motif spacing. A narrow boundary match follows; there is
    no paper-color fade that would interrupt the regular grid during scrolling.
    """
    periods = []
    for axis in (0, 1):
        extent = image.shape[axis]
        # The small game-item marks use a two-cell cycle; include that longer
        # period as well as the underlying diamond spacing.
        candidates = range(max(8, extent // 12), max(9, extent // 3))
        scores = []
        for period in candidates:
            difference = image[period:, :, :] - image[:-period, :, :] if axis == 0 else image[:, period:, :] - image[:, :-period, :]
            scores.append((float(np.abs(difference).mean()), period))
        periods.append(min(scores)[1])
    height, width = image.shape[:2]
    nominal_height = ((height - 1) // periods[0]) * periods[0]
    nominal_width = ((width - 1) // periods[1]) * periods[1]
    crops = []
    for dh in range(-2, 3):
        for dw in range(-2, 3):
            h, w = nominal_height + dh, nominal_width + dw
            if h < 2 or w < 2 or h > height or w > width:
                continue
            y, x = (height - h) // 2, (width - w) // 2
            crop = image[y:y + h, x:x + w]
            score = float(np.abs(crop[0] - crop[-1]).mean() + np.abs(crop[:, 0] - crop[:, -1]).mean())
            crops.append((score, x, y, w, h))
    _, x, y, w, h = min(crops)
    return image[y:y + h, x:x + w].copy(), {
        "sourceWidth": width, "sourceHeight": height,
        "detectedPeriodX": periods[1], "detectedPeriodY": periods[0],
        "crop": {"x": x, "y": y, "width": w, "height": h},
    }


def single_cell_repeat(original: Image.Image, cells_per_axis: int) -> tuple[np.ndarray, dict]:
    """Scale the complete generated cell, match its boundary, then copy it exactly.

    No motif is drawn, cropped out, recolored, denoised or added here. Nearest
    sampling preserves the source's stepped pixel edges. The only color changes
    are the already-established narrow opposite-edge matching corrections.
    """
    if not 2 <= cells_per_axis <= 12:
        raise ValueError("cells-per-axis must be between 2 and 12")
    width, height = original.size
    if width != height:
        raise ValueError(f"A single-cell source must be square, received {width}x{height}")
    resized = original.convert("RGB").resize((UNIT_SIZE, UNIT_SIZE), Image.Resampling.NEAREST)
    unit = np.asarray(resized, dtype=np.float32)
    equalize_edge_pixels(unit, axis=1, band=12)
    equalize_edge_pixels(unit, axis=0, band=12)
    unit_pixels = np.rint(np.clip(unit, 0, 255)).astype(np.uint8)
    edges_equal = np.array_equal(unit_pixels[:, 0, :], unit_pixels[:, -1, :]) and np.array_equal(
        unit_pixels[0, :, :], unit_pixels[-1, :, :])
    if not edges_equal:
        raise ValueError("Single-cell opposite edges do not match exactly")

    pixels = np.tile(unit_pixels, (cells_per_axis, cells_per_axis, 1))
    unit_hash = hashlib.sha256(unit_pixels.tobytes()).hexdigest()
    hashes = set()
    uniform = True
    for row in range(cells_per_axis):
        for column in range(cells_per_axis):
            cell = pixels[row * UNIT_SIZE:(row + 1) * UNIT_SIZE,
                          column * UNIT_SIZE:(column + 1) * UNIT_SIZE]
            hashes.add(hashlib.sha256(cell.tobytes()).hexdigest())
            uniform = uniform and np.array_equal(cell, unit_pixels)
    if not uniform or hashes != {unit_hash}:
        raise ValueError("Repeated cells differ from the single finished source cell")
    return pixels, {
        "sourceDims": {"width": width, "height": height},
        "unitWidth": UNIT_SIZE, "unitHeight": UNIT_SIZE,
        "cellsX": cells_per_axis, "cellsY": cells_per_axis,
        "uniqueUnits": len(hashes), "unitHash": unit_hash,
        "unitHashEncoding": "sha256 of row-major RGB uint8 pixels",
        "strictUniformUnits": bool(uniform), "edgesEqual": bool(edges_equal),
        "resampling": "nearest", "edgeMatchBand": 12,
        "technicalOperations": ["complete-source-scale", "opposite-edge-match", "identical-cell-copy"],
    }


def native_grid_repeat(original: Image.Image, cells_per_axis: int,
                       crop: dict | None = None) -> tuple[np.ndarray, dict]:
    """Copy one complete approved lattice period without scaling its geometry.

    A rectangular period contains two staggered diamonds and their solid center
    squares. Only the established narrow opposite-edge correction changes any
    source pixels; the interior, source texture and palette remain intact.
    """
    if not 2 <= cells_per_axis <= 12:
        raise ValueError("cells-per-axis must be between 2 and 12")
    crop = dict(GRID_SOURCE_CROP if crop is None else crop)
    if set(crop) != {"x", "y", "width", "height"} or any(
            type(value) is not int for value in crop.values()):
        raise ValueError("A grid crop requires integer x, y, width and height")
    x, y, width, height = (crop[key] for key in ("x", "y", "width", "height"))
    if (width, height) != GRID_UNIT_SIZE or x < 0 or y < 0 or (
            x + width > original.width or y + height > original.height):
        raise ValueError(f"A grid crop must be a complete native 260x262 unit inside {original.size}: {crop}")
    source_pixels = np.asarray(original.convert("RGB"), dtype=np.float32)
    luminance = (source_pixels[:, :, 0] * 0.2126 + source_pixels[:, :, 1] * 0.7152
                 + source_pixels[:, :, 2] * 0.0722)
    measured = {}
    for axis, reference, label in ((1, width, "X"), (0, height, "Y")):
        scores = []
        for period in range(reference - 12, reference + 13):
            difference = (luminance[:, period:] - luminance[:, :-period]
                          if axis == 1 else luminance[period:] - luminance[:-period])
            scores.append((float(np.abs(difference).mean()), period))
        score, period = min(scores)
        measured[f"observedPeriod{label}"] = period
        measured[f"observedPeriod{label}MeanAbsoluteLuminanceDifference"] = score
    unit = source_pixels[y:y + height, x:x + width].copy()
    original_unit = unit.copy()
    equalize_edge_pixels(unit, axis=1, band=12)
    equalize_edge_pixels(unit, axis=0, band=12)
    unit_pixels = np.rint(np.clip(unit, 0, 255)).astype(np.uint8)
    edges_equal = np.array_equal(unit_pixels[:, 0], unit_pixels[:, -1]) and np.array_equal(
        unit_pixels[0], unit_pixels[-1])
    interior_unchanged = np.array_equal(unit_pixels[12:-12, 12:-12], original_unit[12:-12, 12:-12])
    if not edges_equal or not interior_unchanged:
        raise ValueError("Native grid edge matching changed the interior or failed to join opposite edges")
    pixels = np.tile(unit_pixels, (cells_per_axis, cells_per_axis, 1))
    rgb_change = np.abs(unit_pixels - original_unit)
    boundary = np.ones((height, width), dtype=bool)
    boundary[12:-12, 12:-12] = False
    unit_hash = hashlib.sha256(unit_pixels.tobytes()).hexdigest()
    hashes = set()
    for row in range(cells_per_axis):
        for column in range(cells_per_axis):
            cell = pixels[row * height:(row + 1) * height, column * width:(column + 1) * width]
            if not np.array_equal(cell, unit_pixels):
                raise ValueError("Copied native grid units differ")
            hashes.add(hashlib.sha256(cell.tobytes()).hexdigest())
    return pixels, {
        "sourceDims": {"width": original.width, "height": original.height},
        "crop": crop, "referencePeriodX": width, "referencePeriodY": height,
        **measured,
        "periodMeasurementMethod": "minimum mean absolute luminance translation difference within reference period +/-12px",
        "unitWidth": width, "unitHeight": height,
        "cellsX": cells_per_axis, "cellsY": cells_per_axis,
        "repeatUnitsX": cells_per_axis, "repeatUnitsY": cells_per_axis,
        "repeatUnitCount": cells_per_axis * cells_per_axis,
        "motifsPerUnit": 2, "filledDiamondCount": 2 * cells_per_axis * cells_per_axis,
        "uniqueUnits": len(hashes), "unitHash": unit_hash,
        "unitHashEncoding": "sha256 of row-major RGB uint8 pixels",
        "strictUniformUnits": hashes == {unit_hash}, "edgesEqual": bool(edges_equal),
        "nativeInteriorUnchanged": bool(interior_unchanged),
        "unitMeanAbsoluteRgbChange": float(rgb_change.mean()),
        "boundaryMeanAbsoluteRgbChange": float(rgb_change[boundary].mean()),
        "resampling": "none-native-pixels", "edgeMatchBand": 12,
        "technicalOperations": ["complete-period-crop", "opposite-edge-match", "identical-unit-copy"],
    }


def build(source: Path, destination: Path, *, legacy: bool = False, pattern: bool = False,
          single_cell: bool = False, native_grid: bool = False,
          cells_per_axis: int = 6, crop: dict | None = None) -> dict:
    with Image.open(source) as original:
        if native_grid:
            pixels, details = native_grid_repeat(original, cells_per_axis, crop)
        elif single_cell:
            pixels, details = single_cell_repeat(original, cells_per_axis)
        else:
            image = np.asarray(original.convert("RGB"), dtype=np.float32)
            details = {}
            if pattern:
                image, details = periodic_crop(image)
            elif legacy:
                image = crossfade_edges(image, axis=1, fraction=0.22)
                image = crossfade_edges(image, axis=0, fraction=0.18)
            else:
                image = finish_calm_edges(image)
            equalize_edge_pixels(image, axis=1, band=12 if pattern else 24)
            equalize_edge_pixels(image, axis=0, band=12 if pattern else 32)
            pixels = np.rint(np.clip(image, 0, 255)).astype(np.uint8)

    # Unity's RawImage repeats in both directions; verify the actual byte values.
    assert np.array_equal(pixels[:, 0, :], pixels[:, -1, :]), destination
    assert np.array_equal(pixels[0, :, :], pixels[-1, :, :]), destination
    Image.fromarray(pixels).save(destination, optimize=True)
    if native_grid:
        # Verify exported PNG pixels as well as the pre-export array.
        with Image.open(destination) as exported:
            exported_pixels = np.asarray(exported.convert("RGB"))
        if not np.array_equal(exported_pixels, pixels):
            raise ValueError(f"Exported native grid PNG pixels changed: {destination}")
        details["exportedPixelsVerified"] = True
    print(f"{destination.name}: {pixels.shape[1]}x{pixels.shape[0]}, both seams exact")
    return {
        "source": str(source.relative_to(ROOT)),
        "destination": str(destination.relative_to(ROOT)),
        "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "runtimeSha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
        "width": int(pixels.shape[1]),
        "height": int(pixels.shape[0]),
        "oppositeEdgePixelsEqual": True,
        "tileFinishingMode": "native-grid-unit-repeat" if native_grid else "single-cell-repeat" if single_cell else "periodic-crop-and-edge-match" if pattern else "legacy-middle-wrap" if legacy else "calm-edge-feather",
        **details,
    }


def restore_legacy() -> None:
    """Restore exact saved runtime bytes, including their original Unity GUIDs."""
    manifest = json.loads((LEGACY / "manifest.json").read_text())
    # Validate every backup before touching the runtime assets.
    for entry in manifest["runtimeFiles"]:
        backup = LEGACY / "runtime" / Path(entry["path"]).name
        assert hashlib.sha256(backup.read_bytes()).hexdigest() == entry["sha256"], backup
    for entry in manifest["runtimeFiles"]:
        backup = LEGACY / "runtime" / Path(entry["path"]).name
        shutil.copyfile(backup, ROOT / entry["path"])
    print(f"Restored {len(manifest['runtimeFiles'])} exact runtime/metadata files from {manifest['head']}")


def restore_pattern_v5() -> None:
    """Restore the twelve approved Pattern-v5 runtime/metadata backup files."""
    validated_runtime_backup(PATTERN_V5_BACKUP, "Pattern-v5", restore=True)
    print("Restored 12 exact Pattern-v5 runtime/metadata files")


def validated_runtime_backup(backup_root: Path, source_set: str, *, restore: bool = False) -> dict:
    """Check the complete runtime whitelist and every hash before any restore."""
    manifest = json.loads((backup_root / "manifest.json").read_text())
    if manifest.get("sourceSet") != source_set:
        raise ValueError(f"Expected {source_set} runtime backup: {backup_root}")
    entries = manifest["runtimeFiles"]
    expected = {str((DESTINATION / filename).relative_to(ROOT)) + suffix
                for filename in BACKGROUNDS.values() for suffix in ("", ".meta")}
    if len(entries) != 12 or {entry["path"] for entry in entries} != expected:
        raise ValueError(f"{source_set} backup must contain exactly the six runtime PNGs and their six .meta files")
    validated = []
    for entry in entries:
        backup = backup_root / "runtime" / Path(entry["path"]).name
        if hashlib.sha256(backup.read_bytes()).hexdigest() != entry["sha256"]:
            raise ValueError(f"{source_set} backup SHA-256 mismatch: {backup}")
        validated.append((backup, ROOT / entry["path"]))
    # No runtime file is touched until every expected backup has been checked.
    if restore:
        for backup, destination in validated:
            shutil.copyfile(backup, destination)
    return manifest


def backup_pattern_v6() -> dict:
    """Preserve the frozen twelve V6 bytes before the first V7 asset build."""
    if (PATTERN_V6_BACKUP / "manifest.json").exists():
        return validated_runtime_backup(PATTERN_V6_BACKUP, "Pattern-v6")
    parity_path = PATTERN_V6_SNAPSHOT / "source-parity.json"
    parity = json.loads(parity_path.read_text())
    if parity["sourceSet"] != "Pattern-v6":
        raise ValueError("The runtime backup must come from the frozen Pattern-v6 snapshot")
    expected = {str((DESTINATION / filename).relative_to(ROOT)) + suffix
                for filename in BACKGROUNDS.values() for suffix in ("", ".meta")}
    entries = [entry for entry in parity["files"] if entry["path"] in expected]
    if len(entries) != 12 or {entry["path"] for entry in entries} != expected:
        raise ValueError("Frozen Pattern-v6 snapshot must contain all twelve runtime/metadata paths")
    validated = []
    for entry in entries:
        data = (PATTERN_V6_SNAPSHOT / "Source" / entry["path"]).read_bytes()
        if hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise ValueError(f"Frozen Pattern-v6 snapshot SHA-256 mismatch: {entry['path']}")
        validated.append((entry, data))
    # Read and check every frozen file before creating the immutable backup.
    (PATTERN_V6_BACKUP / "runtime").mkdir(parents=True, exist_ok=True)
    for entry, data in validated:
        (PATTERN_V6_BACKUP / "runtime" / Path(entry["path"]).name).write_bytes(data)
    manifest = {
        "sourceSet": "Pattern-v6", "baseHead": parity["baseHead"],
        "snapshotRoot": str(PATTERN_V6_SNAPSHOT.relative_to(ROOT)),
        "sourceParityManifestSha256": hashlib.sha256(parity_path.read_bytes()).hexdigest(),
        "runtimeFiles": [{"path": entry["path"], "sha256": entry["sha256"], "bytes": len(data)}
                         for entry, data in validated],
        "restore": "python3 Tools/Art/build_menu_backgrounds.py --restore-pattern-v6",
        "scope": "Exact frozen V6 PNG and .meta bytes; production code and Scene are separately preserved.",
    }
    (PATTERN_V6_BACKUP / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    return validated_runtime_backup(PATTERN_V6_BACKUP, "Pattern-v6")


def restore_pattern_v6() -> None:
    """Restore the frozen V6 runtime pixels and their original Unity GUIDs."""
    validated_runtime_backup(PATTERN_V6_BACKUP, "Pattern-v6", restore=True)
    print("Restored 12 exact Pattern-v6 runtime/metadata files")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-set", choices=("Pattern-v7", "Pattern-v6", "Pattern-v5", "Pattern-v4", "Retro-v3", "Multiverse-v2", "legacy-source"), default="Pattern-v7")
    parser.add_argument("--cells-per-axis", type=int, choices=range(2, 13), default=6,
                        help="Pattern-v6/v7: identical complete units per axis (default: 6)")
    restores = parser.add_mutually_exclusive_group()
    restores.add_argument("--restore-legacy", action="store_true", help="Restore the exact saved legacy runtime PNGs and .meta files")
    restores.add_argument("--restore-pattern-v5", action="store_true", help="Restore the exact approved Pattern-v5 runtime PNGs and .meta files")
    restores.add_argument("--restore-pattern-v6", action="store_true", help="Restore the exact frozen Pattern-v6 runtime PNGs and .meta files")
    parser.add_argument("--report", type=Path, help="Write source/runtime hashes and seam checks to this JSON file")
    args = parser.parse_args()
    if args.restore_legacy:
        restore_legacy()
        raise SystemExit(0)
    if args.restore_pattern_v5:
        restore_pattern_v5()
        raise SystemExit(0)
    if args.restore_pattern_v6:
        restore_pattern_v6()
        raise SystemExit(0)
    source_dir = SOURCE if args.source_set == "legacy-source" else SOURCE / args.source_set
    crops = {name: dict(GRID_SOURCE_CROP) for name in BACKGROUNDS}
    if args.source_set == "Pattern-v7":
        manifest = json.loads((source_dir / "source-manifest.json").read_text())
        entries = {entry["theme"]: entry for entry in manifest["entries"]}
        if len(manifest["entries"]) != 6 or set(entries) != set(BACKGROUNDS):
            raise ValueError("Pattern-v7 source manifest must describe all six themes exactly once")
        for name, entry in entries.items():
            source = source_dir / f"{name}.png"
            if hashlib.sha256(source.read_bytes()).hexdigest() != entry["sourceSha"]:
                raise ValueError(f"Pattern-v7 source SHA-256 mismatch: {source}")
            crops[name] = entry.get("runtimeCrop", dict(GRID_SOURCE_CROP))
    # Fail before any asset writes if generation of the six-set is incomplete.
    for name in BACKGROUNDS:
        with Image.open(source_dir / f"{name}.png") as original:
            original.load()
            if args.source_set == "Pattern-v6" and original.width != original.height:
                raise ValueError(f"Pattern-v6 source must be square: {name}, {original.size}")
            if args.source_set == "Pattern-v7":
                # Finish and validate all six units before writing the first Asset.
                native_grid_repeat(original, args.cells_per_axis, crops[name])
    metadata = {name: (DESTINATION / (filename + ".meta")).read_bytes() for name, filename in BACKGROUNDS.items()}
    if args.source_set == "Pattern-v7":
        backup_pattern_v6()
    results = []
    for name, filename in BACKGROUNDS.items():
        result = build(source_dir / f"{name}.png", DESTINATION / filename,
                       legacy=args.source_set == "legacy-source", pattern=args.source_set in ("Pattern-v4", "Pattern-v5"),
                       single_cell=args.source_set == "Pattern-v6", native_grid=args.source_set == "Pattern-v7",
                       cells_per_axis=args.cells_per_axis, crop=crops[name])
        assert (DESTINATION / (filename + ".meta")).read_bytes() == metadata[name], name
        result["theme"] = name
        result["metadataUnchanged"] = True
        results.append(result)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps({"sourceSet": args.source_set, "textures": results}, indent=2) + "\n")

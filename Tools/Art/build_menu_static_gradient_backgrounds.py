"""Install the approved static pixel gradient without altering its image pixels.

Uses only the Python standard library. A dry run is the default; --apply performs
the six byte-for-byte PNG copies and the scoped importer changes. Unity materials
provide the five alternate palettes, so this tool never redraws or recolors art.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct
from datetime import datetime, timezone


ROOT = Path(__file__).resolve().parents[2]
SOURCE_SET = ROOT / "Tools/ArtSources/MenuBackgrounds/StaticPixelGradient-v1"
APPROVED = ROOT / "output/artproposals/PixelGradient-Red-to-Black-Preview-20261003.png"
PINNED = SOURCE_SET / "approved-red.png"
SOURCE_SHA256 = "d69af0e4378b3b61abd9eb3b1481faa9ce2135c619cae7e42218bf2b3e7b753f"
SOURCE_SIZE = (942, 1670)
FROZEN = ROOT / "Logs/MenuStaticPixelGradientReview/Before/manifest.json"
DESTINATION = ROOT / "Assets/OrganizedResources/InUse/ExternalAssets/bg/bg_main"
BACKUP = SOURCE_SET / "runtime-backup"
BACKUP_MANIFEST = BACKUP / "manifest.json"
MANIFEST = SOURCE_SET / "manifest.json"
SHADER_PATH = "Assets/MainSceneSystem/StaticPixelGradientUI.shader"
SHADER_GUID = "4de53e6b211c4c3180cda34df221a14a"

BACKGROUNDS = {
    "red": ("haku1034_neon8bits2dpixelatedsimpleblackdark_redwarmgamecartoon_b2820319-6293-4895-98f5-ccb12908afc7.png", "eeefd2f03ff5f4541bbbef04d1738e92"),
    "green": ("haku1034_ancient_leaf8bits2dpixelatedsimpleblackdark_greencolda_55806a54-6561-4472-96b9-bce79487ea03.png", "e8e966676cb5e46fdb42579bb766cc9d"),
    "blue": ("haku1034_ancient_pattern8bits2dpixelatedsimpleblackcoldabstract_62782879-5a4b-4676-bdcd-4b40d2ff98b9.png", "b3fe5a1162ddb4abb81010d278b820c7"),
    "light": ("haku1034_ancient_pattern8bits2dpixelatedsimpleblackabstractflat_0378dac9-3b6d-4058-b449-3866dcab9c0e.png", "ac31607d78a8a4677b2dd1a525c3e5da"),
    "dark": ("haku1034_neon8bits2dpixelatedsimpleblack_and_whiteabstractflat_374dc487-6147-4697-bff4-9c1b3e821f70.png", "404e72dee7db1419299acd1370911f1a"),
    "neutral": ("haku1034_lighting_skyin_the_style_of_2d_game_art2dsimplecartoon_04dc2ebb-8c63-4475-a089-21d04afd9ae3 (1).png", "532a8e9fc730c44af96fd53fbe0a41d8"),
}
PALETTES = {
    "red": {"srgbVector": [1.0, 0.0, 0.0], "preserveSource": True, "materialGuid": "0ec943ad094d431a97af0eac47d1ec54"},
    "green": {"srgbVector": [0.12, 0.60, 0.22], "preserveSource": False, "materialGuid": "673c0d6ef6ee4477b8272cae66f238a1"},
    "blue": {"srgbVector": [0.10, 0.32, 0.78], "preserveSource": False, "materialGuid": "429004f572464d74b8ab2aa791c1f9d2"},
    "light": {"srgbVector": [0.82, 0.58, 0.10], "preserveSource": False, "materialGuid": "7c4f852465bf40f1936c01d98345032d"},
    "dark": {"srgbVector": [0.48, 0.13, 0.65], "preserveSource": False, "materialGuid": "be4db68e24eb4eea9df58996ddcbd802"},
    "neutral": {"srgbVector": [0.56, 0.56, 0.56], "preserveSource": False, "materialGuid": "b779056d5a7d4bf1b0c3d8380507f433"},
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def entry(path):
    data = path.read_bytes()
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(data), "bytes": len(data)}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def approved_bytes():
    path = PINNED if PINNED.is_file() else APPROVED
    data = path.read_bytes()
    if sha(data) != SOURCE_SHA256:
        raise ValueError(f"Approved source hash changed: {path}")
    if data[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", data[16:24]) != SOURCE_SIZE:
        raise ValueError("Approved source is not the pinned 942 x 1670 PNG")
    return data


def importer_plan(data, expected_guid):
    text = data.decode("utf-8")
    match = re.search(r"^guid: ([a-f0-9]{32})$", text, re.MULTILINE)
    if not match or match.group(1) != expected_guid:
        raise ValueError("Background texture GUID changed")
    fields = {
        "enableMipMap": 0, "sRGBTexture": 1, "linearTexture": 0,
        "filterMode": 0, "wrapU": 0, "wrapV": 1,
        "nPOTScale": 0, "maxTextureSize": 2048,
        "textureCompression": 0, "textureFormat": -1,
        "crunchedCompression": 0, "overridden": 0,
    }
    for field, value in fields.items():
        pattern = rf"^(\s*{field}: )[-\d]+$"
        text, count = re.subn(pattern, lambda m: m.group(1) + str(value), text, flags=re.MULTILINE)
        if count == 0:
            raise ValueError(f"Importer field missing: {field}")
    if re.search(r"^\s*textureType: 8$", text, re.MULTILINE) is None:
        raise ValueError("Unexpected background texture type; preserve Sprite/RawImage references")
    return text.encode("utf-8")


def prepare():
    """Validate all twelve current runtime inputs before writing any of them."""
    art = approved_bytes()
    frozen = json.loads(FROZEN.read_text())
    frozen_by_path = {f["path"]: f for f in frozen["files"]}
    saved = json.loads(BACKUP_MANIFEST.read_text()) if BACKUP_MANIFEST.is_file() else None
    saved_by_path = {f["path"]: f for f in saved["files"]} if saved else {}
    plan = []
    for element, (name, guid) in BACKGROUNDS.items():
        image = DESTINATION / name
        meta = image.with_suffix(image.suffix + ".meta")
        for path in (image, meta):
            relative = str(path.relative_to(ROOT))
            if relative not in frozen_by_path:
                raise ValueError(f"Not captured in pre-integration frozen manifest: {relative}")
            old = saved_by_path.get(relative, frozen_by_path[relative])
            current = path.read_bytes()
            if path == image:
                result = art
            else:
                original = (BACKUP / path.name).read_bytes() if saved else current
                result = importer_plan(original, guid)
            if sha(current) not in (old["sha256"], sha(result)):
                raise ValueError(f"Concurrent or unexpected asset edit: {relative}")
            if not saved and sha(current) != old["sha256"]:
                raise ValueError(f"Initial assets must still match frozen source: {relative}")
            if saved:
                backup_path = BACKUP / path.name
                if sha(backup_path.read_bytes()) != old["sha256"]:
                    raise ValueError(f"Runtime backup hash changed: {backup_path}")
            plan.append({"element": element, "path": path, "before": current, "after": result,
                         "original": old, "textureGuid": guid})
    return art, plan, saved


def capture_backup(plan):
    BACKUP.mkdir(parents=True, exist_ok=True)
    files = []
    for item in plan:
        path = item["path"]
        backup_path = BACKUP / path.name
        if backup_path.exists():
            if backup_path.read_bytes() != item["before"]:
                raise ValueError(f"Refusing to overwrite backup: {backup_path}")
        else:
            backup_path.write_bytes(item["before"])
        files.append({**item["original"], "backupPath": str(backup_path.relative_to(ROOT)),
                      "copyVerified": sha(backup_path.read_bytes()) == item["original"]["sha256"]})
    write_json(BACKUP_MANIFEST, {
        "schemaVersion": 1, "utcTime": datetime.now(timezone.utc).isoformat(),
        "fileCount": len(files), "allCopiesVerified": all(f["copyVerified"] for f in files),
        "scope": "Six previous looping Pattern-v7 runtime PNGs and their unchanged-GUID importer files",
        "fullIntegrationBackup": str(FROZEN.relative_to(ROOT)),
        "restorationNote": "Runtime restore changes only these twelve files. Full integration rollback also requires Scene/Aspect/configuration restoration from the frozen Before manifests.",
        "files": files,
    })


def material_path(element):
    return ROOT / f"Assets/MainSceneSystem/StaticPixelGradient{element.title()}.mat"


def runtime_manifest(plan):
    backgrounds = []
    for element, (name, guid) in BACKGROUNDS.items():
        image = DESTINATION / name
        mat = material_path(element)
        backgrounds.append({"element": element, "texture": entry(image),
                            "importer": entry(image.with_suffix(".png.meta")),
                            "textureGuid": guid, "materialPath": str(mat.relative_to(ROOT)),
                            "material": entry(mat), "materialImporter": entry(mat.with_suffix(".mat.meta")),
                            **PALETTES[element]})
    return {
        "schemaVersion": 1, "utcTime": datetime.now(timezone.utc).isoformat(),
        "approvedSource": {**entry(PINNED), "dimensions": list(SOURCE_SIZE),
                           "libraryFileId": "libfile_49a5b68988b48191ac3c744444912f9e", "libraryVersion": 3,
                           "approval": "2026-10-03 08:06:51 UTC user: 可以; near-black 1-2 code-value residue accepted"},
        "installation": "Byte-for-byte copies only; no resize, crop, tile, redraw, image edit or recolor",
        "paletteRendering": "Red uses original sampled RGB; alternate materials multiply display-sRGB source red intensity by a fixed Vector palette, with exact sRGB conversion in Linear projects",
        "importerContract": {"pointFilter": True, "wrapU": "Repeat", "wrapV": "Clamp", "mipmaps": False,
                             "npotScale": "None", "compression": "None", "maxTextureSize": 2048,
                             "platformOverrides": False, "preservedGuids": True},
        "allSixTextureFilesMatchApprovedBytes": all(b["texture"]["sha256"] == SOURCE_SHA256 for b in backgrounds),
        "shaderPath": SHADER_PATH, "shaderGuid": SHADER_GUID,
        "shader": entry(ROOT / SHADER_PATH), "shaderImporter": entry(ROOT / (SHADER_PATH + ".meta")),
        "runtimeRestoreManifest": str(BACKUP_MANIFEST.relative_to(ROOT)),
        "fullIntegrationBackup": str(FROZEN.relative_to(ROOT)),
        "sceneAndMotionOwnership": "This copier does not edit Scene, Aspect script, scrolling components, other materials, packages or tests",
        "backgrounds": backgrounds,
    }


def apply():
    art, plan, saved = prepare()
    # Finish all backups before the first destination write. Existing backups are immutable.
    if not saved:
        capture_backup(plan)
    if not PINNED.exists():
        PINNED.parent.mkdir(parents=True, exist_ok=True)
        PINNED.write_bytes(art)
    elif PINNED.read_bytes() != art:
        raise ValueError("Pinned source changed")
    # Recheck captured current inputs after backup I/O to catch concurrent edits.
    for item in plan:
        if item["path"].read_bytes() != item["before"]:
            raise ValueError(f"Asset changed during backup: {item['path']}")
    for item in plan:
        if item["before"] != item["after"]:
            item["path"].write_bytes(item["after"])
    manifest = runtime_manifest(plan)
    write_json(MANIFEST, manifest)
    return {"applied": True, "copiedTextureCount": 6, "validatedInputCount": len(plan),
            "unchangedGuidCount": 6, "manifest": str(MANIFEST.relative_to(ROOT)),
            "allSixTextureFilesMatchApprovedBytes": manifest["allSixTextureFilesMatchApprovedBytes"]}


def check():
    _, plan, _ = prepare()
    passed = all(p["path"].read_bytes() == p["after"] for p in plan)
    result = {"passed": passed, "validatedRuntimeInputCount": len(plan),
              "approvedDimensions": list(SOURCE_SIZE), "approvedSha256": SOURCE_SHA256,
              "matches": [{"path": str(p["path"].relative_to(ROOT)),
                           "matchesPlannedBytes": p["path"].read_bytes() == p["after"]} for p in plan]}
    if not passed:
        raise ValueError("Runtime files do not match approved static installation")
    return result


def restore_runtime():
    _, plan, saved = prepare()
    if not saved:
        raise ValueError("No prior runtime snapshot to restore")
    # prepare() validates every current input and every backup before any writes.
    for item in plan:
        backup_path = BACKUP / item["path"].name
        shutil.copyfile(backup_path, item["path"])
    return {"restoredRuntimeFileCount": len(plan), "guidCount": 6,
            "note": "Scene/Aspect/material binding restoration is separate; see full frozen Before manifests."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument("--apply", action="store_true")
    actions.add_argument("--check", action="store_true")
    actions.add_argument("--restore-runtime", action="store_true")
    args = parser.parse_args()
    if args.apply:
        result = apply()
    elif args.check:
        result = check()
    elif args.restore_runtime:
        result = restore_runtime()
    else:
        _, plan, saved = prepare()
        result = {"dryRun": True, "writes": False, "approvedDimensions": list(SOURCE_SIZE),
                  "approvedSha256": SOURCE_SHA256, "validatedRuntimeInputCount": len(plan),
                  "backupAlreadyCaptured": saved is not None,
                  "plannedTextureCopies": 6, "plannedImporterEdits": 6}
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

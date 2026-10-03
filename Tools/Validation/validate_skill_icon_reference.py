#!/usr/bin/env python3
"""Verify registered skill sprites against the pinned MCombat reference pixels.

Requires Pillow. Does not start Unity, change imports, or modify the reference.
Pass --reference to additionally verify the authoritative project's current
Addressables GUID mappings and image bytes rather than a same-named file.
"""

import argparse
import csv
import hashlib
import json
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageOps


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def entries(root):
    text = (root / "Assets/AddressableAssetsData/AssetGroups/SkillIcon.asset").read_text()
    blocks = re.findall(r"^  - m_GUID: ([a-f0-9]+)\n(.*?)(?=^  - m_GUID:|^  m_ReadOnly:|\Z)", text, re.S | re.M)
    result = {}
    for guid, block in blocks:
        match = re.search(r"^    m_Address: (\d+)\s*$", block, re.M)
        if not match:
            continue
        skill_id = match.group(1)
        assert "    - skill_icon\n" in block, f"Missing skill_icon label: {skill_id}"
        result.setdefault(skill_id, []).append(guid)
    return result


def check_pixels(path, record):
    with Image.open(path) as source:
        image = source.convert("RGBA")
        assert image.size == (record["width"], record["height"]), f"Image dimensions differ: {path}"
        assert sha256(image.tobytes()) == record["rgbaSha256"], f"MCombat RGBA pixels differ: {path}"


def thumbnail(records, root, reference, output):
    selected = [record for record in records if record["replaced"]]
    canvas = Image.new("RGB", (680, len(selected) * 150 + 40), "#152030")
    draw = ImageDraw.Draw(canvas)
    draw.text((100, 12), "MCombat registered reference", fill="white")
    draw.text((420, 12), "PocketStriker replacement", fill="white")
    for row, record in enumerate(selected):
        draw.text((8, 60 + row * 150), record["id"], fill="white")
        for column, path in enumerate((reference / record["referencePath"], root / record["path"])):
            with Image.open(path) as source:
                image = ImageOps.contain(source.convert("RGBA"), (128, 128))
                canvas.paste(image, (100 + column * 320, 45 + row * 150), image)
    canvas.save(output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, help="Read-only path to the authoritative MCombat project")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    manifest = json.loads((root / "Tools/Validation/skill_icon_reference.json").read_text())
    records = manifest["icons"]
    current = entries(root)
    assert all(len(guids) == 1 for guids in current.values()), "PocketStriker has duplicate skill icon addresses"
    expected_ids = {record["id"] for record in records}
    with (root / "Assets/ExternalAssets/Config/mst_skill.csv").open(encoding="utf-8-sig") as source:
        skills = {row["id"]: row["REAL_NAME"] for row in csv.DictReader(source)}
    assert set(current) == expected_ids == set(skills), "Skill table and registered icon IDs do not match the reference"
    source_entries = entries(args.reference) if args.reference else None
    if source_entries is not None:
        assert set(source_entries) == expected_ids, "Reference project's registered icon IDs changed"
        assert {skill_id: guids for skill_id, guids in source_entries.items() if len(guids) > 1} == manifest["referenceDuplicateAddresses"], \
            "Reference project's duplicate icon bindings changed"
    for record in records:
        skill_id = record["id"]
        path = root / record["path"]
        meta_path = Path(str(path) + ".meta")
        assert skills[skill_id] == record["realName"], f"Skill ID changed meaning: {skill_id}"
        assert current[skill_id] == [record["guid"]], f"Addressables binding changed: {skill_id}"
        assert re.search(r"^guid: " + record["guid"] + r"$", meta_path.read_text(), re.M), f"Asset GUID changed: {skill_id}"
        assert sha256(meta_path.read_bytes()) == record["metaSha256"], f"Original sprite import configuration changed: {skill_id}"
        assert sha256(path.read_bytes()) == record["fileSha256"], f"Pinned icon file changed: {skill_id}"
        check_pixels(path, record)
        if args.reference:
            reference_path = args.reference / record["referencePath"]
            assert source_entries[skill_id][0] == record["referenceGuid"], f"Reference binding changed: {skill_id}"
            assert re.search(r"^guid: " + record["referenceGuid"] + r"$",
                             Path(str(reference_path) + ".meta").read_text(), re.M), f"Wrong registered reference path: {skill_id}"
            assert sha256(reference_path.read_bytes()) == record["referenceFileSha256"], f"Reference icon changed: {skill_id}"
            check_pixels(reference_path, record)
    assert sorted(record["id"] for record in records if record["replaced"]) == sorted(manifest["replacedIds"])
    output = root / "Logs/SkillIconReference"
    output.mkdir(parents=True, exist_ok=True)
    if args.reference:
        thumbnail(records, root, args.reference, output / "comparison-after.png")
    report = {"passed": True, "registeredIcons": len(records), "replacedIds": manifest["replacedIds"],
              "referenceMappingsChecked": source_entries is not None,
              "referenceDuplicateAddresses": manifest["referenceDuplicateAddresses"],
              "referenceNote": manifest["referenceNote"],
              "checks": ["skill IDs and real names", "registered Addressables GUIDs and labels",
                         "original sprite metadata", "pinned file bytes and MCombat RGBA pixels"],
              "replacements": [record for record in records if record["replaced"]]}
    (output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(f"PASS: {len(records)} skill icon pixels, import settings and GUID mappings; "
          f"{len(manifest['replacedIds'])} replacements match the MCombat reference.")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Validate production attempt seeds and inspect remote resource wiring without paid requests.

Unity-only prompt/payload/ownership fixtures live in PocketStrikerStoryVarietyValidation.
This runner compiles the real seed selector and checks the authored remote assets.
"""
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def unity_fields(path):
    """Read the scalar/list subset used by these serialized Unity asset assertions."""
    fields = {}
    active = None
    for line in path.read_text().splitlines():
        key = re.match(r"^  ([A-Za-z_][A-Za-z_0-9]*):(?: (.*))?$", line)
        if key:
            active, value = key.group(1), key.group(2) or ""
            fields[active] = value
        elif active and line.startswith("  - "):
            if not isinstance(fields[active], list):
                fields[active] = []
            value = line[4:]
            fields[active].append(json.loads(value) if value.startswith('"') else value)
    return fields


def guid(path):
    return re.search(r"^guid: ([0-9a-f]+)$", path.with_name(path.name + ".meta").read_text(), re.M).group(1)


def validate_assets(root):
    config_path = root / "Assets/AIStory/AIServiceConfig.asset"
    fallback_path = root / "Assets/AIStory/FairyTaleFallbackConfig.asset"
    templates_path = root / "Assets/AIStory/PocketStrikerStoryPrompts.json"
    config = unity_fields(config_path)
    fallback = unity_fields(fallback_path)
    templates = json.loads(templates_path.read_text())
    require(config["storyThemes"] == "[]" and config["pageCount"] == "1" and config["storyStyle"] == "0",
            "MCombat narrative theme/page/style defaults changed.")
    require(config["customStoryStylePrompt"] == "" and config["additionalStoryRequirements"] == "",
            "PocketStriker added narrative rules outside the MCombat defaults.")
    require(config["imageStyle"] == "7" and "cartoon" in config["customImageStylePrompt"]
            and config["imageAspectRatio"] == "9:16", "Cartoon portrait image settings are missing.")
    require(config["fallbackTone"] == "0" and config["fairyTaleFallbackConfigAddress"] == "Config/FairyTaleFallbackConfig"
            and guid(fallback_path) in config["fairyTaleFallbackConfig"], "Active fallback resource reference is missing.")
    require(templates["schemaVersion"] == 1, "Unsupported prompt resource schema.")
    require({entry["language"] for entry in templates["locales"]} == {"Chinese", "Japanese", "English"},
            "A MCombat prompt locale is missing.")
    for entry in templates["locales"]:
        for placeholder in ("storyTheme", "pageCount", "storyStyle", "imageStyle", "languageInstruction"):
            require("{" + placeholder + "}" in entry["template"], f"Remote template misses {placeholder}.")
        for json_field in ("characters", "locations", "style", "scenes", "dialogues", "visualPrompt"):
            require('"' + json_field + '"' in entry["template"], f"Remote template misses MCombat field {json_field}.")
    require("{language}" in templates["otherLanguageInstruction"], "Other-language instruction is missing.")
    require(templates["image"]["overviewMaxCharacters"] == 500 and "watermark" in templates["image"]["defaultNegativeTokens"],
            "MCombat image prompt limits/default negatives changed.")
    for field in ("settings", "worldDetails", "heroes", "companions", "goals", "conflicts", "resolutions"):
        require(isinstance(fallback[field], list) and len(fallback[field]) > 1, f"Fallback collection is incomplete: {field}")
    shared_fallback = (root / "Packages/com.mcombat.shared/Runtime/AIStory/Services/FairyTaleFallbackConfig.cs").read_text()
    default_expression = re.search(r"private const string DefaultStyleInstructions =\s*(.*?);\n", shared_fallback, re.S).group(1)
    default_guidance = "".join(json.loads('"' + value + '"')
                               for value in re.findall(r'"((?:[^"\\]|\\.)*)"', default_expression))
    require(json.loads(fallback["styleGuidance"]) == default_guidance,
            "Remote fallback style guidance differs from the active MCombat default.")
    group = (root / "Assets/AddressableAssetsData/AssetGroups/Config.asset").read_text()
    entries = dict((address, value) for value, address in re.findall(r"  - m_GUID: ([0-9a-f]+)\n    m_Address: (.+)\n", group))
    for address, path in (("Config/AIServiceConfig", config_path), ("Config/PocketStrikerStoryPrompts", templates_path),
                          ("Config/FairyTaleFallbackConfig", fallback_path)):
        require(entries.get(address) == guid(path), f"Remote Addressables mapping is missing: {address}")
    settings = (root / "Assets/AddressableAssetsData/AddressableAssetSettings.asset").read_text()
    schema = (root / "Assets/AddressableAssetsData/AssetGroups/Schemas/Config_BundledAssetGroupSchema.asset").read_text()
    for field, profile in (("m_BuildPath", "Remote.BuildPath"), ("m_LoadPath", "Remote.LoadPath")):
        profile_id = re.search(r"  " + field + r":\n    m_Id: ([0-9a-f]+)", schema).group(1)
        require(re.search(r"m_Id: " + profile_id + r"\n      m_Name: " + re.escape(profile), settings) is not None,
                f"Config group is not wired to {profile}.")
    require("  m_IncludeInBuild: 1" in schema and "  m_IncludeAddressInCatalog: 1" in schema,
            "Remote config resources are excluded from the content catalog.")
    source = (root / "Assets/AIStory/PocketStrikerStoryVariety.cs").read_text()
    require(not any(symbol in source for symbol in ("BuildTextPrompt", "BuildImagePrompt", "CartoonStyle", "Themes", "Twists", "Tones")),
            "Attempt selector still contains hard-coded narrative/art rules.")
    mcombat_root = Path(os.environ.get("MCOMBAT_ROOT", str(root.parent / "MComat"))).expanduser()
    reference_config_path = mcombat_root / "Assets/AIStory/AIServiceConfig.asset"
    reference_fallback_path = mcombat_root / "Assets/AIStory/FairyTaleFallbackConfig.asset"
    if reference_config_path.is_file() and reference_fallback_path.is_file():
        reference_config = unity_fields(reference_config_path)
        reference_fallback = unity_fields(reference_fallback_path)
        for field in ("currentModel", "storyThemes", "pageCount", "additionalImageRequirements", "fallbackTone", "fairyTaleFallbackConfigAddress"):
            require(config[field] == reference_config[field], f"Active MCombat config differs in {field}.")
        for field in ("settings", "worldDetails", "heroes", "companions", "goals", "conflicts", "resolutions"):
            require(fallback[field] == reference_fallback[field], f"Remote MCombat fallback content differs in {field}.")
        require(guid(fallback_path) != guid(reference_fallback_path), "PocketStriker fallback reuses the MCombat asset identity.")
        print("Remote story assets: PASS (MCombat source parity, independent asset identity, localized schema, remote Addressables wiring).")
    else:
        print("Remote story assets: PASS (localized schema, active default guidance, remote wiring; external MCombat source unavailable).")


def main():
    root = Path(__file__).resolve().parents[2]
    validate_assets(root)
    version = next(line.split(":", 1)[1].strip()
                   for line in (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    editor = Path(os.environ.get("UNITY_EDITOR_PATH", f"/Applications/Unity/Hub/Editor/{version}/Unity.app")).expanduser()
    if editor.name == "Unity":
        editor = editor.parents[2]
    mono_root = editor / "Contents/Resources/Scripting/MonoBleedingEdge"
    mono = mono_root / "bin/mono"
    compiler = mono_root / "lib/mono/4.5/csc.exe"
    for dependency in (mono, compiler):
        require(dependency.is_file(), f"Missing Unity compiler runtime: {dependency}")
    with tempfile.TemporaryDirectory(prefix="pocketstriker-story-variety-") as directory:
        executable = Path(directory) / "StoryVarietyTests.exe"
        subprocess.run([
            str(mono), str(compiler), "/nologo", "/langversion:9.0", f"/out:{executable}",
            str(root / "Assets/AIStory/PocketStrikerStoryVariety.cs"), str(root / "Tools/Validation/StoryVarietyTests.cs"),
        ], check=True, cwd=root)
        subprocess.run([str(mono), str(executable)], check=True, cwd=root, timeout=30)


if __name__ == "__main__":
    main()

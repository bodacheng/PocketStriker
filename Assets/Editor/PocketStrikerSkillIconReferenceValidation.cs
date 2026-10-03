using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>Confirm replaced reference images remain usable through the production sprite addresses.</summary>
public static class PocketStrikerSkillIconReferenceValidation
{
    [Serializable] sealed class Manifest { public Icon[] icons; }
    [Serializable] sealed class Icon
    {
        public string id, path, guid;
        public int width, height;
        public bool replaced;
    }
    [Serializable] sealed class Report
    {
        public bool passed;
        public int registeredSprites, replacedSprites;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Skill Icon MCombat Reference")]
    public static void Validate()
    {
        var report = new Report();
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/Validation/skill_icon_reference.json"));
        if (settings == null || manifest?.icons == null) throw new InvalidOperationException("Missing skill icon mappings/reference manifest.");
        foreach (var icon in manifest.icons)
        {
            var entry = settings.FindAssetEntry(icon.guid);
            if (entry == null || entry.address != icon.id || !entry.labels.Contains("skill_icon") || entry.AssetPath != icon.path)
            {
                report.errors.Add(icon.id + ": production Addressables mapping differs from the retained GUID/path.");
                continue;
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.AssetPath);
            if (sprite == null || sprite.texture == null)
            {
                report.errors.Add(icon.id + ": registered sprite did not import.");
                continue;
            }
            report.registeredSprites++;
            if (!icon.replaced) continue;
            var importer = AssetImporter.GetAtPath(entry.AssetPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single || !importer.alphaIsTransparency)
                report.errors.Add(icon.id + ": sprite type or transparent import settings changed.");
            if (Mathf.Abs(sprite.rect.width - icon.width) > .01f || Mathf.Abs(sprite.rect.height - icon.height) > .01f)
                report.errors.Add(icon.id + ": imported sprite rectangle does not cover the complete reference image: " + sprite.rect);
            report.replacedSprites++;
        }
        report.passed = report.errors.Count == 0 && report.registeredSprites == 96 && report.replacedSprites == 7;
        report.checks.AddRange(new[] { "96 production sprite addresses resolve retained GUIDs", "7 reference replacements import with full image rectangles and transparency" });
        Directory.CreateDirectory("Logs/SkillIconReference");
        File.WriteAllText("Logs/SkillIconReference/unity-import-report.json", JsonUtility.ToJson(report, true));
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_SKILL_ICON_REFERENCE_PASSED: 96 registered sprites; seven replacements including native TIFF import.");
    }
}

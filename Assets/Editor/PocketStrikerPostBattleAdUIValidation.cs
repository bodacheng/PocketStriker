using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Loads the production result prefab and checks its advertisement-free reward rows.</summary>
public static class PocketStrikerPostBattleAdUIValidation
{
    [Serializable]
    public sealed class Report
    {
        public bool passed;
        public int rewardRowsChecked;
        public int checks;
        public List<string> errors = new List<string>();
    }

    [MenuItem("PocketStriker/Validation/Post Battle Advertisement UI")]
    public static void Validate()
    {
        var report = Run();
        if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
        Debug.Log("POCKETSTRIKER_POST_BATTLE_AD_UI_PASSED: " + report.checks + " checks.");
    }

    public static Report Run()
    {
        var report = new Report();
        try
        {
            void Require(bool valid, string message)
            {
                report.checks++;
                if (!valid) throw new InvalidOperationException(message);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DummyLayerSystem/ArenaFightOver.prefab");
            Require(prefab != null, "The result prefab is missing.");
            Require(prefab.GetComponentsInChildren<AdmobAdsButton>(true).Length == 0,
                "A manual ad placement remains in the result prefab.");
            Require(prefab.GetComponentsInChildren<OpenNoAdsPurchase>(true).Length == 0,
                "A remove-ads purchase button remains in the result prefab.");
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Require(transform.name != "AdsT" && transform.name != "watchAd" && transform.name != "noAds",
                    "An obsolete ad or purchase UI container remains: " + transform.name);
            var layer = prefab.GetComponent<ArenaFightOver>();
            foreach (var name in new[] { "dmParent", "gdParent" })
            {
                var row = Field<RectTransform>(layer, name);
                var icon = (RectTransform)row.Find("Icon");
                var total = (RectTransform)row.Find("Count");
                var reward = (RectTransform)row.Find("Reward");
                Require(icon != null && total != null && reward != null, "A result currency row is incomplete: " + name);
                var iconBounds = Bounds(icon, row);
                var totalBounds = Bounds(total, row);
                var rewardBounds = Bounds(reward, row);
                Require(Contains(row.rect, iconBounds) && Contains(row.rect, totalBounds) && Contains(row.rect, rewardBounds),
                    "A currency icon, total or award escapes its row: " + name);
                Require(iconBounds.xMax < totalBounds.xMin && totalBounds.xMax < rewardBounds.xMin,
                    "Currency icon, total and award overlap: " + name);
                if (name == "dmParent")
                {
                    var vip = Field<RectTransform>(layer, "vipSymbol");
                    var vipBounds = Bounds(vip, row);
                    Require(Contains(row.rect, vipBounds) && rewardBounds.xMax < vipBounds.xMin,
                        "The entitlement label overlaps the award or escapes the result row.");
                }
                report.rewardRowsChecked++;
            }
            report.passed = true;
        }
        catch (Exception error) { report.errors.Add(error.ToString()); }
        return report;
    }

    static T Field<T>(object owner, string name) where T : class =>
        typeof(ArenaFightOver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) as T;

    static Rect Bounds(RectTransform item, RectTransform reference)
    {
        var corners = new Vector3[4];
        item.GetWorldCorners(corners);
        var min = (Vector2)reference.InverseTransformPoint(corners[0]);
        var max = (Vector2)reference.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f
        && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;
}

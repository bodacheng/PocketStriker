using System.Collections.Generic;
using Skill;
using UnityEngine;

/// <summary>
/// Player-facing skill copy uses MCombat's skill_name.csv first. Metadata comes
/// from mst_skill.csv and skill_ai_attrs.csv; display estimates do not define it.
/// </summary>
public static class SkillDescription
{
    public static string TapHint => Localize("Tap a gem for skill details", "タップしてスキル詳細", "点击技能石查看说明");
    public static string DetailsTitle => Localize("SKILL DETAILS", "スキル詳細", "技能说明");
    public static string PreviewLabel => Localize("Preview", "動きを見る", "查看演示");
    public static string CloseLabel => Localize("Close", "閉じる", "关闭");

    public static string GetName(SkillConfig config)
    {
        if (config == null) return string.Empty;
        var name = SkillNameTable.GetSkillNameOrDefault(config.RECORD_ID, config.REAL_NAME);
        return string.IsNullOrWhiteSpace(name) ? config.REAL_NAME : name;
    }

    public static string GetIntro(SkillConfig config)
    {
        if (config == null) return string.Empty;
        var authored = SkillNameTable.GetSkillIntro(config.RECORD_ID);
        if (!string.IsNullOrWhiteSpace(authored)) return authored.Trim();

        // Future server-defined skills still get a useful, factual explanation.
        // Do not promise status effects, hit counts or damage without authored copy.
        return GetCategory(config) + " · " + GetSpecialTier(config) + "\n" + GetRange(config);
    }

    public static string GetCategory(SkillConfig config)
    {
        if (config == null) return string.Empty;
        switch (config.STATE_TYPE)
        {
            case BehaviorType.GR:
            case BehaviorType.GI:
            case BehaviorType.GM:
            case BehaviorType.GMB:
                return Localize("Attack", "攻撃技", "攻击技");
            case BehaviorType.CT:
            case BehaviorType.RB:
            case BehaviorType.Def:
                return Localize("Defensive skill", "防御技", "防御技");
            case BehaviorType.MV:
            case BehaviorType.AC:
                return Localize("Movement", "移動技", "移动技");
            default:
                return Localize("Skill", "技能", "技能");
        }
    }

    public static string GetSpecialTier(SkillConfig config)
    {
        if (config == null) return string.Empty;
        return config.SP_LEVEL > 0 ? "EX " + config.SP_LEVEL
            : Localize("Normal", "通常技", "普通技");
    }

    public static string GetRange(SkillConfig config)
    {
        var attrs = config?.AIAttrs;
        var ranges = new List<string>(3);
        if (attrs != null)
        {
            if (SkillConfig.RangeLimit(attrs.AI_MIN_DIS, attrs.AI_MAX_DIS, true, false, false))
                ranges.Add(Localize("Close", "近距離", "近距离"));
            if (SkillConfig.RangeLimit(attrs.AI_MIN_DIS, attrs.AI_MAX_DIS, false, true, false))
                ranges.Add(Localize("Mid", "中距離", "中距离"));
            if (SkillConfig.RangeLimit(attrs.AI_MIN_DIS, attrs.AI_MAX_DIS, false, false, true))
                ranges.Add(Localize("Far", "遠距離", "远距离"));
        }

        var label = Localize("Auto-use distance: ", "自動発動距離：", "自动出招距离：");
        return label + (ranges.Count > 0 ? string.Join(" / ", ranges)
            : Localize("Unspecified", "指定なし", "未指定"));
    }

    public static string GetMetadata(SkillConfig config) =>
        GetCategory(config) + " · " + GetSpecialTier(config) + "\n" + GetRange(config);

    static string Localize(string english, string japanese, string chinese)
    {
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.Japanese: return japanese;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return chinese;
            default: return english;
        }
    }
}

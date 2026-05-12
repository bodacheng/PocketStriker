using UnityEngine;

public static class FightGlobalSettingUtility
{
    public static float CalculateScaledValue(
        float coefficient,
        float originValue,
        float level,
        float levelPowerAverager,
        float levelDiminishStart,
        float levelDiminishingRange)
    {
        return coefficient * originValue * CalcLevelMultiplier(
            level,
            levelPowerAverager,
            levelDiminishStart,
            levelDiminishingRange);
    }

    public static float CalcLevelMultiplier(
        float level,
        float levelPowerAverager,
        float levelDiminishStart,
        float levelDiminishingRange)
    {
        if (level <= 1f || levelPowerAverager <= 0f)
            return Mathf.Max(level, 0f);

        var extraLevels = level - 1f;
        var startExtraLevels = Mathf.Max(levelDiminishStart - 1f, 0f);
        if (extraLevels <= startExtraLevels)
            return 1f + extraLevels * levelPowerAverager;

        var bonusBeforeDiminish = startExtraLevels * levelPowerAverager;
        var beyondLevels = extraLevels - startExtraLevels;
        var slowFactor = 1f + beyondLevels / Mathf.Max(levelDiminishingRange, 0.01f);
        var diminishedBonus = beyondLevels * (levelPowerAverager / slowFactor);
        return 1f + bonusBeforeDiminish + diminishedBonus;
    }

    public static string EffectPathDefine(Element element = Element.Null)
    {
        switch (element)
        {
            case Element.blueMagic:
                return "bluemagic";
            case Element.redMagic:
                return "redmagic";
            case Element.greenMagic:
                return "greenmagic";
            case Element.lightMagic:
                return "lightmagic";
            case Element.darkMagic:
                return "darkmagic";
            case Element.Null:
            default:
                return "defaultmagic";
        }
    }
}

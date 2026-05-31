using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "MatureStoryFallbackConfig", menuName = "AI Story/Mature Story Fallback Config")]
public class MatureStoryFallbackConfig : StoryFallbackConfigBase
{
    private static readonly string[] DefaultSettingsData =
    {
        "崩塌的神庙遗址",
        "熔岩裂谷边的黑曜祭坛",
        "血月下的古城断壁",
        "雾罩的高塔修道院",
        "倒悬在沙海上的金字塔内室",
        "漂浮在深渊之上的断桥群岛",
        "荆棘环绕的远古战场",
        "龙骨堆砌的地下王座厅",
        "潮湿幽暗的墓穴长廊",
        "星辉照耀的废弃天文台",
        "被藤蔓缠绕的石像林中庭",
        "火山口边缘的亡国圣城"
    };

    private static readonly string[] DefaultWorldDetailsData =
    {
        "神纹石柱断裂，火把在硫磺雾中忽明忽暗",
        "祭坛石面刻满献祭痕迹，熔岩热浪烘烤皮肤",
        "墙壁浮雕残留古文明的战争与欲望故事",
        "修道院钟声回荡，低语与祷告交织成压迫感",
        "金字塔内壁渗出冷汗般的水滴，符文微光闪烁",
        "深渊气流掠过断桥，链条与骨铃发出清脆声",
        "枯旱荆棘上挂着破碎战旗，地面遍布裂痕与焦黑",
        "龙骨反射暗红火光，空气中有铁锈与血的味道",
        "墓穴潮气混合香灰气息，石棺缝隙透出蓝色灵火",
        "星光穿透穹顶裂缝，古仪器仍缓缓转动",
        "石像表面布满蔓藤与苔藓，似乎在夜里移动视线",
        "火山灰飘落如雪，城墙裂缝里渗出炭火微光"
    };

    private static readonly string[] DefaultHeroesData =
    {
        "赤足披短斗篷的年轻勇者，肌肉线条紧绷",
        "刻有符文绷带的炼金修行僧，肩颈裸露",
        "以链甲披肩遮背的舞刃刺客，动作妖娆",
        "银发女剑圣，皮革短袍勾勒身形",
        "火纹刺青的武僧，手持长棍，胸腹裸露",
        "占星僧侣少女，贴身护符环绕锁骨",
        "被诅咒的王子，裂口礼袍下露出锁链",
        "深色皮肤的弓手，紧身革衣随动作起伏",
        "海潮气息的女海盗冒险者，短装湿漉漉",
        "魇梦巫师学徒，轻薄法袍半掩纹身",
        "沙漠游侠修女，金属臂环与裸足共鸣",
        "双刀武僧少女，腰间铃铛摇曳"
    };

    private static readonly string[] DefaultCompanionsData =
    {
        "，与会低语的石像鬼作伴",
        "，由半兽祭司引路",
        "，带着被封印的魔剑作为契约伙伴",
        "，驯服一只吐息灼热的幼龙",
        "，受魅魔间谍暗中协助",
        "，与失忆的圣骑士互相背靠",
        "，肩上停着预言的三眼乌鸦",
        "，被刻着锁链纹身的信徒护送",
        "，牵着由灵火组成的仆从",
        "，与擅长破阵的符文匠结伴",
        "，背着绑有圣油的铁链鞭",
        "，与地下角斗士交换誓约同行"
    };

    private static readonly string[] DefaultGoalsData =
    {
        "潜入魔王宫殿摧毁心脏祭坛",
        "从血祭台抢回被献祭的同伴",
        "夺回圣器以换取村庄赎金",
        "破解古碑咒文阻止瘟疫蔓延",
        "寻找失落龙脉之钥复活旧王",
        "推翻被腐化的大祭司",
        "护送圣物穿越诅咒峡谷",
        "打断黑日仪式让天空重启",
        "与魔王决斗换取俘虏释放",
        "潜入密牢救出被俘的修女勇者",
        "在废墟城中建立新的盟约",
        "阻止恶魔军团跨界入侵"
    };

    private static readonly string[] DefaultConflictsData =
    {
        "仪式提前启动，魔力暴走",
        "队伍被锁链分割，各自孤立",
        "同伴被魅惑反叛，必须制服而不致死",
        "魔王部下布下诱捕阵，路线被迫更改",
        "在重伤与流血中继续战斗",
        "诅咒让感官迟钝，视线模糊",
        "敌人用俘虏威胁迫降，选择受辱或反抗",
        "魔力反噬导致幻觉与迷失",
        "古机关需要鲜血或献祭才能开启",
        "救人与击杀敌人的目标发生冲突",
        "魔王亲临，压迫感使人战栗",
        "内心欲望与誓约互相撕扯"
    };

    private static readonly string[] DefaultResolutionsData =
    {
        "击碎祭坛但魔力泄洪，半数人被诅咒",
        "斩杀敌首却暴露踪迹，引来更大追猎",
        "屈辱交易换回俘虏，尊严与信任破碎",
        "被俘后留下复仇誓言，等待下一次反攻",
        "牺牲自己封印魔王，伙伴含泪撤退",
        "勉强逃离但圣物遗失，危机延续",
        "被迫臣服魔王苟活，心中暗藏野望",
        "在废墟上立下残酷的新秩序，善恶模糊",
        "拯救成功却付出无法逆转的代价"
    };

    private const string DefaultStyleInstructions =
        "请将故事写成{pageCount}个场景的分镜。角色是年轻的勇者、修行僧与魔法战士，男女皆偏向性感，男性普遍光着上身或衣着贴身，女性衣着性感，身上可能有纹身或符文饰品。" +
        "允许锁链、俘虏与失败情节，结局可以残酷或暧昧。";

    protected override string[] DefaultSettings => DefaultSettingsData;
    protected override string[] DefaultWorldDetails => DefaultWorldDetailsData;
    protected override string[] DefaultHeroes => DefaultHeroesData;
    protected override string[] DefaultCompanions => DefaultCompanionsData;
    protected override string[] DefaultGoals => DefaultGoalsData;
    protected override string[] DefaultConflicts => DefaultConflictsData;
    protected override string[] DefaultResolutions => DefaultResolutionsData;
    protected override string DefaultStyleGuidance => DefaultStyleInstructions;

    public static MatureStoryFallbackConfig CreateDefault()
    {
        return CreateDefault<MatureStoryFallbackConfig>();
    }

    public static MatureStoryFallbackConfig CreateMerged(MatureStoryFallbackConfig source)
    {
        return CreateMerged<MatureStoryFallbackConfig>(source);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ApplyDefaultValues();
    }

    [MenuItem("Assets/Create/AI Story/Mature Story Fallback Config (Default)", priority = 0)]
    private static void CreateAssetWithDefaults()
    {
        var asset = CreateDefault();
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Mature Story Fallback Config",
            "MatureStoryFallbackConfig",
            "asset",
            "Select location for the fallback mature story config asset");

        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(asset);
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
#endif
}

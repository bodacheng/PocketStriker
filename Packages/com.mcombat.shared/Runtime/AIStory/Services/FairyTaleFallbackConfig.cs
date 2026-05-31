using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "FairyTaleFallbackConfig", menuName = "AI Story/Fairy Tale Fallback Config")]
public class FairyTaleFallbackConfig : StoryFallbackConfigBase
{
    private static readonly string[] DefaultSettingsData =
    {
        "雲海に浮かぶ星明かりの森",
        "深海サンゴに隠された瑠璃の王国",
        "千羽鶴で組まれた空中市場",
        "陽が沈まない琥珀の谷",
        "歌う花びらが敷き詰められた小さな町",
        "月光で織られた宙吊りの滝",
        "雲鯨が見守る虹色の湖",
        "古代の亀甲と歩調を合わせて進む砂州都市",
        "北極のオーロラに漂うガラス温室の群島",
        "古い時計塔の内側に隠された螺旋都市",
        "巨樹の根が支える地下の光の庭",
        "時間の砂で鋳造された時計荒野"
    };
    
    private static readonly string[] DefaultWorldDetailsData =
    {
        "住民は流星を織りながら暮らし、街路は潮の満ち引きで移ろう",
        "潮騒の楽器と古代の神殿が溶け合い、夜ごとに新しい歴史が映し出される",
        "紙の翼に支えられた市で、人々は夢と記憶を取引する",
        "九時間ごとに季節が巡る空の下で、作物は雲とともに育つ",
        "湖面は未来を映し、岸辺には記憶の灯籠が漂う",
        "草木が旋律を奏で、山並みがリズムで呼応する",
        "建物は呼吸し、窓は訪れた者の約束を記録する",
        "時計は語られた物語で動き、沈黙すれば時間も止まる",
        "旅人は風を手紙にし、便箋は光る小さな蝶へと変わる",
        "黎明には星々と位置を入れ替え、世界は二重の光と影をまとう"
    };
    
    private static readonly string[] DefaultHeroesData =
    {
        "夢を織ることを学ぶ若き徒弟",
        "勇敢な小さな薬師",
        "発明好きな孤児の木工師",
        "凧祭りを守る番人",
        "心優しい貝殻集めの人",
        "星と語り合う航海少女",
        "勇敢な洞窟の灯り手",
        "伝承の記録を守る雲上の司書",
        "旅する香料画家",
        "極光砂漠から来た天文航法士",
        "時間の花を研究する植物学の達人",
        "風の楽譜を綴る歴史見習い",
        "潮汐の舞を織り上げる吟遊学者",
        "嵐の記憶を書き留める兄妹の見張り人",
        "異なる星港から来た二人の通信使",
        "季節と契約した三人の徒弟",
        "夢のアーカイブを守る若きキュレーターたち",
        "種族を越えた盟約の小隊で、各自が異なる元素を司る"
    };
    
    private static readonly string[] DefaultCompanionsData =
    {
        "、言葉を話す石像の猫と共に",
        "、いたずら好きな風の精霊と旅する",
        "、光を放つタンポポ灯を携えて",
        "、不思議な折鶴の導きで進む",
        "、森を守る角の少年と同行する",
        "、歌声に秀でた潮の姉妹と協力する",
        "、千年眠っていた星塵の狐と手を取り合う",
        "、感情を写し取る水紋の幻蝶と旅路を共にする",
        "、香りを記譜する青い蔦の楽譜の助けを得る",
        "、昼夜の刻を背負う甲虫の使者と組む",
        "、変幻する光影の木偶を守りながら進む",
        "、未来を描く流光のタコと同行する",
        "、記憶を呼び覚ますプリズムのヒバリを連れる",
        "、夢を紡ぐ糸の竜と結びつく",
        "、感情を訳す振り子の花狐と協力する",
        "、光を運ぶ雨燕のキャラバンに守られて雨の中を進む",
        "、星火に変わる瑠璃の蜂の群れを肩に乗せて",
        "、風の音を刻む砂礫の巨人に従い旅する",
        "、昼と夜の物語が宿る双面の仮面と共に行動する",
        "、失われた足音を集める街灯の童子と同行する",
        "、空間を折り畳むヒヤシンス折り紙師と組む",
        "、腹掛け姿で火を噴く少年とペアを組む"
    };
    
    private static readonly string[] DefaultGoalsData =
    {
        "忘れ去られた季節の時計を取り戻す",
        "壊れた月のつり橋を修復する",
        "村の星光の果実を守る",
        "夢に囚われた友を救い出す",
        "眠る守護神の樹を目覚めさせる",
        "古い楽章の最後の小節を奏で切る",
        "迷子になった暁をもう一度昇らせる",
        "人界と夢界を結ぶ灯火祭を準備する",
        "声を失った山々に歌を取り戻させる",
        "生まれたばかりの雲竜の棲み処を探す",
        "二つの村の季節争いを仲裁する",
        "消えゆく風の物語を記録する",
        "夜行の生き物と昼の住民が同じ約束を分かち合えるよう手助けする"
    };
    
    private static readonly string[] DefaultConflictsData =
    {
        "記憶を変えてしまう川を渡らねばならない",
        "旅人を惑わす是非の風と対話しなければならない",
        "影が織り上げた迷宮の試練に挑む",
        "目覚めて怒る巨石の守護者をなだめる必要がある",
        "映り込む影に隠された謎を解かねばならない",
        "時間を凍らせる月光の霧を突き抜ける必要がある",
        "自ら書き換わる予言の巻物を理解する必要がある",
        "語り合う月影と陽光の対立を調停する必要がある",
        "異なる種族の伝統への固執と不安に向き合わねばならない",
        "書き換わり続ける歴史絵巻を整理し真実を探す",
        "時間とリズムを再び揃えなければ世界が傾く",
        "自らの願いと託された期待の均衡を取らなければならない"
    };
    
    private static readonly string[] DefaultResolutionsData =
    {
        "勇気と善意を分かち合い危機を解く",
        "真摯に耳を傾け恐れを乗り越える",
        "答えが心の奥にあったと気づく",
        "友情の光で陰りを払い去る",
        "互いの約束を信じ合えるようになる",
        "誰もが互いに見守り合う意味を知る",
        "希望をより必要とする人へ手渡す",
        "すべての声に耳を傾け円満に収める",
        "伝統と革新の融和にバランスを見出す",
        "共に物語を創り世界を動かし続ける"
    };

    private const string DefaultStyleInstructions =
        "请将故事编排为{pageCount}个连续场景的童话绘本。所有出现的人物都保持亚洲面容，男角色呈现古雅典竞技士风格（光膀子、可能披短斗篷或披肩、赤脚），发型随意(可能寸头或光头或中长度)，" +
        "女角色穿着古雅典短裙与古代饰物，可赤脚或系带凉鞋，整体气质温柔而奇幻。" +
        "每幅插图务必捕捉角色动作进行中的瞬间，突出肢体张力、飘动的衣饰与丰富表情，强调角色与场景元素的互动，使画面与当前剧情进展紧密契合，避免静态站立或摆拍感。";

    protected override string[] DefaultSettings => DefaultSettingsData;
    protected override string[] DefaultWorldDetails => DefaultWorldDetailsData;
    protected override string[] DefaultHeroes => DefaultHeroesData;
    protected override string[] DefaultCompanions => DefaultCompanionsData;
    protected override string[] DefaultGoals => DefaultGoalsData;
    protected override string[] DefaultConflicts => DefaultConflictsData;
    protected override string[] DefaultResolutions => DefaultResolutionsData;
    protected override string DefaultStyleGuidance => DefaultStyleInstructions;

    public static FairyTaleFallbackConfig CreateDefault()
    {
        return CreateDefault<FairyTaleFallbackConfig>();
    }

    public static FairyTaleFallbackConfig CreateMerged(FairyTaleFallbackConfig source)
    {
        return CreateMerged<FairyTaleFallbackConfig>(source);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ApplyDefaultValues();
    }

    [MenuItem("Assets/Create/AI Story/Fairy Tale Fallback Config (Default)", priority = 0)]
    private static void CreateAssetWithDefaults()
    {
        var asset = CreateDefault();
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Fairy Tale Fallback Config",
            "FairyTaleFallbackConfig",
            "asset",
            "Select location for the fallback fairy tale config asset");

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

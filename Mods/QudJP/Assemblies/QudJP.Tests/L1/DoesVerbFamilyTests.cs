using System.Text.Json;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbFamilyTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;
    private string dictionaryDirectory = null!;
    private string leafFilePath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        leafFilePath = Path.Combine(tempDirectory, "ui-messagelog-leaf.ja.json");
        dictionaryDirectory = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));

        File.Copy(
            Path.Combine(dictionaryDirectory, "messages.ja.json"),
            patternFilePath);

        var leafSource = Path.Combine(dictionaryDirectory, "ui-messagelog-leaf.ja.json");
        var worldLeafSource = Path.Combine(dictionaryDirectory, "ui-messagelog-world.ja.json");
        WriteCombinedLeafFile(leafFilePath, leafSource, worldLeafSource);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(
            Path.GetFullPath(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "Localization",
                    "MessageFrames",
                    "verbs.ja.json")));
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        MessagePatternTranslator.SetLeafFileForTests(leafFilePath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    // --- Status Predicate Family ---

    // Plain text (runtime-like already-localized display names)
    [TestCase("The 熊 is exhausted!", "熊は疲弊した！")]
    [TestCase("The 熊 is stuck.", "熊は動けなくなった。")]
    [TestCase("The グロウパッド is sealed.", "グロウパッドは封印された。")]
    [TestCase("You are exhausted!", "あなたは疲弊した！")]
    // Color-wrapped (AddPlayerMessage wraps entire message in {{color|...}})
    [TestCase("{{g|The 熊 is exhausted!}}", "{{g|熊は疲弊した！}}")]
    public void Translate_StatusPredicateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Negation/Lack Family ---

    // Plain text
    [TestCase("The タレット can't hear you!", "タレットにはあなたの声が聞こえない！")]
    [TestCase("The 熊 does not have a consciousness you can make psychic contact with.", "熊には精神的に接触できる意識がない。")]
    [TestCase("You don't penetrate the スナップジョー's armor!", "スナップジョーの防具を貫通できなかった！")]
    [TestCase("You can't see!", "視界がない！")]
    [TestCase("The タレット doesn't have enough charge to function.", "タレットには機能するのに十分な充電がない")]
    // Color-wrapped (ConsequentialColor wraps full message)
    [TestCase("{{r|You don't penetrate the スナップジョー's armor!}}", "{{r|スナップジョーの防具を貫通できなかった！}}")]
    public void Translate_NegationLackFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Combat Damage Family (third-person actor) ---

    // Plain text
    [TestCase("The 熊 hits the スナップジョー for 5 damage!", "熊はスナップジョーに5ダメージを与えた！")]
    [TestCase("The 熊 misses the スナップジョー!", "熊はスナップジョーへの攻撃を外した！")]
    [TestCase("The 熊 hits the スナップジョー with a 青銅の短剣 for 3 damage.", "熊は青銅の短剣でスナップジョーに3ダメージを与えた。")]
    [TestCase("The 熊 misses the スナップジョー with a 青銅の短剣! [8 vs 12]", "熊は青銅の短剣でスナップジョーへの攻撃を外した！ [8 vs 12]")]
    // Penetration color (Stat.GetResultColor wraps full message in ampersand format)
    // AddPlayerMessage converts ampersand to brace: &W → {{W|...}}
    [TestCase("{{W|The 熊 hits the スナップジョー for 5 damage!}}", "{{W|熊はスナップジョーに5ダメージを与えた！}}")]
    [TestCase("{{r|The 熊 misses the スナップジョー!}}", "{{r|熊はスナップジョーへの攻撃を外した！}}")]
    public void Translate_CombatDamageThirdPerson(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Possession/Lack Family ---

    // Plain text
    [TestCase("The 水筒 has no room for more water.", "水筒にはこれ以上の水を入れる余地がない。")]
    [TestCase("You have no more ammo!", "弾薬が尽きた！")]
    [TestCase("The 熊 has nothing to say.", "熊は何も言うことがない。")]
    [TestCase("You have left your party.", "あなたはパーティーを離れた。")]
    // Color-wrapped
    [TestCase("{{y|You have no more ammo!}}", "{{y|弾薬が尽きた！}}")]
    public void Translate_PossessionLackFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Motion/Direction Family ---

    // Plain text
    [TestCase("The 熊 falls to the ground.", "熊は地面に倒れた。")]
    [TestCase("You fall to the ground.", "あなたは地面に倒れた。")]
    [TestCase("The スナップジョー falls asleep.", "スナップジョーは眠りに落ちた。")]
    [TestCase("You fall asleep.", "あなたは眠りに落ちた。")]
    // Color-wrapped (ConsequentialColor)
    [TestCase("{{g|The 熊 falls to the ground.}}", "{{g|熊は地面に倒れた。}}")]
    public void Translate_MotionDirectionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Extended Status/State Family (ARE verb) ---

    [TestCase("The 装置 is utterly unresponsive.", "装置はまったく反応しない")]
    [TestCase("The 装置 is fully charged!", "装置は完全に充電された！")]
    [TestCase("The 熊 is no longer your follower.", "熊はもはやあなたの仲間ではない")]
    [TestCase("The 熊 is now your follower.", "熊はあなたの仲間になった")]
    [TestCase("The 装備品 is ripped from your body!", "装備品があなたの体から引き剥がされた！")]
    [TestCase("The 剣 is pulled toward something.", "剣は何かに引き寄せられた")]
    [TestCase("The 剣 is pulled toward the 熊.", "剣は熊に引き寄せられた")]
    [TestCase("The 扉 is open.", "扉は開いている")]
    [TestCase("The 熊 is not bleeding.", "熊は出血していない")]
    [TestCase("The 武器 is already fully loaded.", "武器はすでに完全に装填されている")]
    [TestCase("The 水筒 is already full of 水.", "水筒はすでに水で満たされている")]
    [TestCase("The 水筒 is already full.", "水筒はすでに満タンだ")]
    [TestCase("The 装置 is turned off.", "装置の電源は切れている")]
    [TestCase("The 炎 is extinguished by the 水.", "炎は水によって消し止められた")]
    [TestCase("The 熊 is covered in sticky goop!", "熊はべとべとの粘液に覆われた！")]
    [TestCase("The 熊 is covered in 酸.", "熊は酸に覆われた")]
    [TestCase("The 装置 is immune to conventional treatments.", "装置は通常の治療が効かない")]
    [TestCase("The 武器 is lost in the goop!", "武器は粘液の中に沈んだ！")]
    [TestCase("The 装置 is still starting up.", "装置はまだ起動中だ")]
    [TestCase("The 装置 is still attuning.", "装置はまだ同調中だ")]
    [TestCase("The 装置 is still cooling down.", "装置はまだ冷却中だ")]
    [TestCase("The 装置 is dead still.", "装置はまったく動かない")]
    [TestCase("The 装置 is in no need of repairs.", "装置は修理の必要がない")]
    [TestCase("The 熊 is unable to consume tonics.", "熊はトニックを摂取できない")]
    [TestCase("The 熊 is too large for you to engulf.", "熊は大きすぎて飲み込めない")]
    [TestCase("The 装置 is out of your telekinetic range.", "装置はあなたの念動力の範囲外だ")]
    // Color-wrapped
    [TestCase("{{r|The 装置 is utterly unresponsive.}}", "{{r|装置はまったく反応しない}}")]
    [TestCase("{{g|The 装置 is fully charged!}}", "{{g|装置は完全に充電された！}}")]
    [TestCase("{{R|The 装備品 is ripped from your body!}}", "{{R|装備品があなたの体から引き剥がされた！}}")]
    public void Translate_ExtendedStatusFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Social/Persuasion Family ---

    [TestCase("The 熊 is unconvinced by your pleas, but interested in hearing more.", "熊はあなたの懇願に納得していないが、もっと聞きたがっている")]
    [TestCase("The 熊 is unconvinced by your pleas.", "熊はあなたの懇願に納得していない")]
    [TestCase("The 熊 is sympathetic, but unable to join you.", "熊は同情的だが、あなたに加わることはできない")]
    [TestCase("The 熊 is offended by your impertinence.", "熊はあなたの無礼に気分を害した")]
    // Color-wrapped
    [TestCase("{{r|The 熊 is unconvinced by your pleas.}}", "{{r|熊はあなたの懇願に納得していない}}")]
    public void Translate_SocialPersuasionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Encoded/Recoiler Family ---

    [TestCase("The リコイラー is encoded with an imprint of the Thin World that has no meaning in the Thick World.", "リコイラーは薄界の刻印で符号化されているが、厚界では意味を成さない")]
    [TestCase("The リコイラー is encoded with an imprint that has no meaning in your present context.", "リコイラーは現在の状況では意味を成さない刻印で符号化されている")]
    [TestCase("The リコイラー is encoded with the imprint of a remote pocket dimension, 秘境, that is inaccessible from your present context.", "リコイラーは遠方のポケット次元秘境の刻印で符号化されているが、現在の状況ではアクセスできない")]
    // Color-wrapped
    [TestCase("{{r|The リコイラー is encoded with an imprint of the Thin World that has no meaning in the Thick World.}}", "{{r|リコイラーは薄界の刻印で符号化されているが、厚界では意味を成さない}}")]
    public void Translate_EncodedRecoilerFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Engaged/Busy Family ---

    [TestCase("The 商人 is engaged in melee combat and is too busy to trade with you.", "商人は近接戦闘中で取引どころではない")]
    [TestCase("The 商人 is engaged in hand-to-hand combat and is too busy to have a conversation with you.", "商人は格闘戦闘中で会話どころではない")]
    [TestCase("The 商人 is on fire and is too busy to trade with you.", "商人は燃えていてそれどころではない")]
    // Color-wrapped
    [TestCase("{{r|The 商人 is engaged in melee combat and is too busy to trade with you.}}", "{{r|商人は近接戦闘中で取引どころではない}}")]
    public void Translate_EngagedBusyFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Not Owned Family ---

    [TestCase("The 武器 is not owned by you, and trying to repair 武器 will be considered an act of theft. Are you sure?", "武器はあなたのものではなく、武器を修理しようとすると窃盗行為とみなされる。本当に行うか？")]
    [TestCase("The 武器 is not owned by you, and using 武器 will be considered an act of theft. Are you sure?", "武器はあなたのものではなく、武器を使用すると窃盗行為とみなされる。本当に行うか？")]
    [TestCase("The 武器 is not owned by you, and examining 武器 will be considered an act of theft. Continue?", "武器はあなたのものではなく、武器を調べると窃盗行為とみなされる。続けるか？")]
    [TestCase("The 水筒 is not owned by you. Are you sure you want to pour from 水筒?", "水筒はあなたのものではない。水筒から注いでよいか？")]
    [TestCase("The 装置 is not owned by you. Are you sure you want to disassemble 装置?", "装置はあなたのものではない。装置を分解してよいか？")]
    // Color-wrapped
    [TestCase("{{r|The 武器 is not owned by you, and trying to repair 武器 will be considered an act of theft. Are you sure?}}", "{{r|武器はあなたのものではなく、武器を修理しようとすると窃盗行為とみなされる。本当に行うか？}}")]
    public void Translate_NotOwnedFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Don't Penetrate Armor Family ---

    [TestCase("The 熊 doesn't penetrate your armor with 爪! [3]", "熊は爪であなたの防具を貫通できなかった！ [3]")]
    [TestCase("The 熊 doesn't penetrate your armor! [5]", "熊はあなたの防具を貫通できなかった！ [5]")]
    [TestCase("The 熊 doesn't penetrate your armor.", "熊はあなたの防具を貫通できなかった")]
    // Color-wrapped
    [TestCase("{{r|The 熊 doesn't penetrate your armor.}}", "{{r|熊はあなたの防具を貫通できなかった}}")]
    public void Translate_DontPenetrateArmorFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Don't Have Enough Charge Family ---

    [TestCase("The 装置 doesn't have enough charge to sustain the field!", "装置にはフィールドを維持するのに十分な充電がない！")]
    [TestCase("The 装置 doesn't have enough charge to operate.", "装置には動作するのに十分な充電がない")]
    [TestCase("The 装置 does not have enough charge to operate.", "装置には動作するのに十分な充電がない")]
    [TestCase("The 装置 doesn't have enough charge to be imprinted with the current location.", "装置には現在地を刻印するのに十分な充電がない")]
    [TestCase("The 装置 doesn't have enough charge to function.", "装置には機能するのに十分な充電がない")]
    // Color-wrapped
    [TestCase("{{r|The 装置 doesn't have enough charge to sustain the field!}}", "{{r|装置にはフィールドを維持するのに十分な充電がない！}}")]
    public void Translate_DontHaveChargeFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Don't Seem / Don't Have (misc) Family ---

    [TestCase("The 熊 doesn't seem to understand you.", "熊はあなたの言葉を理解していないようだ")]
    [TestCase("The 装置 does not seem to be able to float at this time.", "装置は現時点では浮遊できないようだ")]
    [TestCase("The 商人 doesn't have the skill to identify artifacts.", "商人にはアーティファクトを鑑定する技能がない")]
    [TestCase("The 熊 doesn't have anything like a head to conk.", "熊には殴るような頭部がない")]
    [TestCase("The 装置 doesn't have a digital mind you can make electronic contact with.", "装置にはあなたが電子接触できるデジタル精神がない")]
    [TestCase("The 熊 doesn't want a new name.", "熊は新しい名前を望んでいない")]
    [TestCase("The 装置 does nothing.", "装置は何もしなかった")]
    // Color-wrapped
    [TestCase("{{r|The 熊 doesn't seem to understand you.}}", "{{r|熊はあなたの言葉を理解していないようだ}}")]
    public void Translate_DontMiscFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Have (additional) Family ---

    [TestCase("The 商人 has nothing to trade.", "商人には取引するものがない")]
    [TestCase("The 熊 has no limbs that can be amputated.", "熊には切断できる四肢がない")]
    [TestCase("The 熊 has no limbs.", "熊には四肢がない")]
    [TestCase("The 熊 has already boosted immunity from a nostrum.", "熊はすでにノストラムによる免疫増強を受けている")]
    [TestCase("The 水筒 has no drain.", "水筒には排水口がない")]
    [TestCase("The 装置 has been hacked.", "装置はハッキングされた")]
    [TestCase("The 武器 has no more ammo!", "武器の弾薬が尽きた！")]
    [TestCase("The 熊 has left your party.", "熊はパーティーを離れた")]
    // Color-wrapped
    [TestCase("{{r|The 商人 has nothing to trade.}}", "{{r|商人には取引するものがない}}")]
    public void Translate_HaveAdditionalFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Click/Vibrate/Aren't Family ---

    [TestCase("The 装置 merely clicks.", "装置はカチッと鳴るだけだった")]
    [TestCase("The 装置 vibrates slightly.", "装置はわずかに振動した")]
    [TestCase("The 装置 isn't working!", "装置は作動していない！")]
    // Color-wrapped
    [TestCase("{{r|The 装置 merely clicks.}}", "{{r|装置はカチッと鳴るだけだった}}")]
    [TestCase("{{r|The 装置 vibrates slightly.}}", "{{r|装置はわずかに振動した}}")]
    public void Translate_DeviceMalfunctionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Seem Family ---

    [TestCase("The 装置 seems to have taken on new qualities.", "装置は新たな特質を帯びたようだ")]
    [TestCase("The 熊 seems utterly impervious to your charms.", "熊はあなたの魅力にまったく動じない")]
    // Color-wrapped
    [TestCase("{{g|The 装置 seems to have taken on new qualities.}}", "{{g|装置は新たな特質を帯びたようだ}}")]
    public void Translate_SeemFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Share Family ---

    [TestCase("The 長老 shares a recipe with you.", "長老はあなたにレシピを教えた")]
    [TestCase("The 長老 shares some gossip with you.", "長老はあなたに噂話を教えた")]
    [TestCase("The 長老 shares the location of 遺跡.", "長老は遺跡の場所を教えた")]
    [TestCase("The 長老 shares an event from the life of a sultan with you.", "長老はスルタンの人生の出来事をあなたに語った")]
    [TestCase("The 長老 shares the recipe for 秘薬.", "長老は秘薬のレシピを教えた")]
    // Color-wrapped
    [TestCase("{{g|The 長老 shares a recipe with you.}}", "{{g|長老はあなたにレシピを教えた}}")]
    public void Translate_ShareFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Teach Family ---

    [TestCase("The 師匠 teaches you to craft the item modification 強化.", "師匠はアイテム改造強化の製作法をあなたに教えた")]
    [TestCase("The 師匠 teaches you to craft 短剣.", "師匠は短剣の製作法をあなたに教えた")]
    [TestCase("The 師匠 teaches you 剣術!", "師匠はあなたに剣術を教えた")]
    // Color-wrapped
    [TestCase("{{g|The 師匠 teaches you 剣術!}}", "{{g|師匠はあなたに剣術を教えた}}")]
    public void Translate_TeachFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Return/Shower/Emit Family ---

    [TestCase("The 熊 returns to the ground.", "熊は地上に戻った")]
    [TestCase("The 装置 showers sparks everywhere.", "装置はあたりに火花を散らした")]
    [TestCase("The 装置 emits a shower of sparks!", "装置は火花の雨を放った！")]
    [TestCase("The 装置 emits a grinding noise.", "装置は軋む音を発した")]
    // Color-wrapped
    [TestCase("{{g|The 熊 returns to the ground.}}", "{{g|熊は地上に戻った}}")]
    [TestCase("{{r|The 装置 emits a shower of sparks!}}", "{{r|装置は火花の雨を放った！}}")]
    public void Translate_MotionEmissionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Loosen Family ---

    [TestCase("The 甲殻 loosens.", "甲殻が緩んだ")]
    // Color-wrapped
    [TestCase("{{r|The 甲殻 loosens.}}", "{{r|甲殻が緩んだ}}")]
    public void Translate_LoosenFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Say Family ---

    [TestCase("The 熊 says, '挨拶'.", "熊は「挨拶」と言った")]
    // Color-wrapped
    [TestCase("{{g|The 熊 says, '挨拶'.}}", "{{g|熊は「挨拶」と言った}}")]
    public void Translate_SayFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Fail Family ---

    [TestCase("The 熊 fails to deal damage with its attack! [3]", "熊の攻撃はダメージを与えられなかった！ [3]")]
    [TestCase("The 毒針 fails to penetrate 熊's armor and is destroyed.", "毒針は熊の防具を貫通できず、破壊された")]
    // Color-wrapped
    [TestCase("{{r|The 熊 fails to deal damage with its attack! [3]}}", "{{r|熊の攻撃はダメージを与えられなかった！ [3]}}")]
    public void Translate_FailFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Resist Family ---

    [TestCase("The 熊 resists your life drain!", "熊はあなたの生命吸収に抵抗した！")]
    [TestCase("The 熊 resists your shield slam.", "熊はあなたのシールドスラムに抵抗した")]
    // Color-wrapped
    [TestCase("{{r|The 熊 resists your life drain!}}", "{{r|熊はあなたの生命吸収に抵抗した！}}")]
    public void Translate_ResistFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Start Family ---

    [TestCase("The 苔 starts to fizz hungrily.", "苔は飢えたように泡立ち始めた")]
    [TestCase("The 宝石 starts to gleam with an unearthly light.", "宝石はこの世ならぬ光で輝き始めた")]
    // Color-wrapped
    [TestCase("{{g|The 宝石 starts to gleam with an unearthly light.}}", "{{g|宝石はこの世ならぬ光で輝き始めた}}")]
    public void Translate_StartFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Press Family ---

    [TestCase("The 熊 presses your activation panel.", "熊はあなたの起動パネルを押した")]
    [TestCase("The 熊 presses 装置's activation panel.", "熊は装置の起動パネルを押した")]
    // Color-wrapped
    [TestCase("{{r|The 熊 presses your activation panel.}}", "{{r|熊はあなたの起動パネルを押した}}")]
    public void Translate_PressFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Reviewed Does() Families ---

    [TestCase("The 電池 is unpowered.", "電池は無電力だ。")]
    [TestCase("The 商人 ponies up 5 drams of fresh water to even up the trade.", "商人は取引を釣り合わせるために真水を5ドラム支払った。")]
    [TestCase("The 司書 provides some insightful commentary on 古代書.", "司書は古代書について示唆に富む解説をしてくれた。")]
    [TestCase("The 回収装置 reclaims a 金属片.", "回収装置は金属片を回収した。")]
    [TestCase("The 盗賊 bot snags your 光線銃!", "盗賊 botはあなたの光線銃をかすめ取った！")]
    [TestCase("The 装置 needs 3 more rounds before it can be fired again.", "装置は再発射まであと3ラウンド必要だ。")]
    [TestCase("The 商人 is already your follower. Do you want to beguile it anyway?", "商人はすでにあなたの仲間だ。それでも魅了するか？")]
    public void Translate_ReviewedDoesFamilies(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Detach Family ---

    [TestCase("The クローン detaches from you!", "クローンはあなたから分離した！")]
    [TestCase("The クローン detaches from the 熊.", "クローンは熊から分離した")]
    // Color-wrapped
    [TestCase("{{g|The クローン detaches from you!}}", "{{g|クローンはあなたから分離した！}}")]
    public void Translate_DetachFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Cleave Family ---

    [TestCase("The 戦士 cleaves through your 盾.", "戦士はあなたの盾を両断した")]
    [TestCase("The 戦士 cleaves through the 熊's 腕.", "戦士は熊's 腕を両断した")]
    // Color-wrapped
    [TestCase("{{R|The 戦士 cleaves through your 盾.}}", "{{R|戦士はあなたの盾を両断した}}")]
    [TestCase("{{g|The 戦士 cleaves through the 熊's 腕.}}", "{{g|戦士は熊's 腕を両断した}}")]
    public void Translate_CleaveFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Impale Family ---

    [TestCase("The 熊 impales itself on 棘 and takes 5 damage!", "熊は棘に突き刺さり5ダメージを受けた！")]
    // Color-wrapped
    [TestCase("{{G|The 熊 impales itself on 棘 and takes 5 damage!}}", "{{G|熊は棘に突き刺さり5ダメージを受けた！}}")]
    public void Translate_ImpaleFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Kick Family ---

    [TestCase("The 熊 kicks at you, but the kick passes through you.", "熊はあなたを蹴ろうとしたが、蹴りはすり抜けた")]
    [TestCase("The 熊 kicks at you, but you hold your ground.", "熊はあなたを蹴ろうとしたが、あなたは踏みとどまった")]
    [TestCase("The 熊 kicks you backwards.", "熊はあなたを蹴り飛ばした")]
    [TestCase("The 熊 kicks the スナップジョー backwards.", "熊はスナップジョーを蹴り飛ばした")]
    // Color-wrapped
    [TestCase("{{r|The 熊 kicks you backwards.}}", "{{r|熊はあなたを蹴り飛ばした}}")]
    public void Translate_KickFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Reflect Family ---

    [TestCase("The 装甲 reflects 8 damage back at you.", "装甲は8ダメージをあなたに跳ね返した")]
    [TestCase("The 装甲 reflects 8 damage back at the 熊.", "装甲は8ダメージを熊に跳ね返した")]
    // Color-wrapped
    [TestCase("{{R|The 装甲 reflects 8 damage back at you.}}", "{{R|装甲は8ダメージをあなたに跳ね返した}}")]
    public void Translate_ReflectFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Single-Use Verb Family ---

    [TestCase("The 熊 begins flying.", "熊が飛翔し始めた。")]
    [TestCase("The 装置 goes into sleep mode.", "装置はスリープモードに入った")]
    [TestCase("The 熊 winces.", "熊はたじろいだ")]
    [TestCase("The 熊 ignores the 冒険者.", "熊は冒険者を無視した")]
    [TestCase("The 熊 won't let you bandage it.", "熊はあなたに包帯を巻かせない")]
    [TestCase("The 電池 locks firmly into the socket, preventing removal.", "電池はソケットにしっかりと嵌まり、取り外しができなくなった")]
    [TestCase("The 熊 steps on the 装置 and vibrates through spacetime.", "熊は装置を踏み、時空を振動して通り抜けた")]
    [TestCase("The 熊 slouches in pacification and radiates a chord of light.", "熊は鎮静の中でうなだれ、光の和音を放った")]
    [TestCase("The 装置 beeps loudly and flashes a warning glyph.", "装置は大きなビープ音を鳴らし、警告記号を点滅させた")]
    [TestCase("The 彫像 crumbles into beetles.", "彫像は崩れて甲虫の群れになった")]
    [TestCase("The 生物 atomizes and recombines into a 熊.", "生物は原子分解し、熊として再結合した")]
    [TestCase("The 宝石 stops gleaming.", "宝石の輝きが消えた")]
    [TestCase("The 熊 shies away from you.", "熊はあなたから怯えて離れた")]
    [TestCase("The 装置 ceases floating near you.", "装置はあなたの近くで浮遊するのをやめた")]
    [TestCase("The 装置 collapses under the pressure of normality and implodes.", "装置は正常性の圧力に耐えきれず崩壊し、内破した")]
    [TestCase("The 金属片 becomes magnetized!", "金属片は磁化された！")]
    [TestCase("The 熊 vomits everywhere!", "熊はあたりに嘔吐した！")]
    [TestCase("The 装置 attunes to your physiology.", "装置はあなたの生理機能に同調した")]
    [TestCase("The 岩 cannot be moved.", "岩は動かせない")]
    [TestCase("The 熊 joins you!", "熊があなたの仲間に加わった！")]
    [TestCase("The 長老 gifts you a 短剣.", "長老はあなたに短剣を贈った")]
    [TestCase("The 熊 sees no reason for you to amputate its 腕.", "熊はあなたが腕を切断する理由がないと考えている")]
    [TestCase("The 装置 needs to be hung up first.", "装置はまず吊り下げる必要がある")]
    [TestCase("The 装置 needs water in it.", "装置には水が必要だ")]
    [TestCase("The 装置 was cracked.", "装置にひびが入った")]
    // Color-wrapped
    [TestCase("{{g|The 熊 begins flying.}}", "{{g|熊が飛翔し始めた。}}")]
    [TestCase("{{r|The 熊 vomits everywhere!}}", "{{r|熊はあたりに嘔吐した！}}")]
    [TestCase("{{g|The 熊 joins you!}}", "{{g|熊があなたの仲間に加わった！}}")]
    public void Translate_SingleUseVerbFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Try Family ---

    [TestCase("The 熊 tries to engulf you, but fails.", "熊はあなたを飲み込もうとしたが、失敗した")]
    [TestCase("The 熊 tries to engulf the スナップジョー, but fails.", "熊はスナップジョーを飲み込もうとしたが、失敗した")]
    // Color-wrapped
    [TestCase("{{r|The 熊 tries to engulf you, but fails.}}", "{{r|熊はあなたを飲み込もうとしたが、失敗した}}")]
    public void Translate_TryFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Ask Family ---

    [TestCase("The 熊 asks about its location and is no longer lost.", "熊は自分の居場所について尋ね、もう迷っていない")]
    // Color-wrapped
    [TestCase("{{g|The 熊 asks about its location and is no longer lost.}}", "{{g|熊は自分の居場所について尋ね、もう迷っていない}}")]
    public void Translate_AskFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Pony Up / Disintegrate / Shout Family ---

    [TestCase("The 商人 ponies up 5 drams of 水.", "商人は水を5ドラム支払った")]
    [TestCase("Just before your demise, you are transported to safety! The リコイラー disintegrates.", "死の寸前で安全な場所へ転送された！ リコイラーは崩壊した")]
    // Color-wrapped
    [TestCase("{{g|The 商人 ponies up 5 drams of 水.}}", "{{g|商人は水を5ドラム支払った}}")]
    public void Translate_TransactionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Vital Area Hit Family ---

    [TestCase("You hit the スナップジョー in a vital area.", "スナップジョーの急所に命中した")]
    [TestCase("The 熊 hits the スナップジョー in a vital area.", "熊がスナップジョーの急所に命中させた")]
    // Color-wrapped
    [TestCase("{{g|You hit the スナップジョー in a vital area.}}", "{{g|スナップジョーの急所に命中した}}")]
    public void Translate_MissileVitalAreaFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Hit with Projectile Family (Player attacker) ---

    [TestCase("You critically hit the 熊 (x3) with a 短弓 for 15 damage!", "短弓で熊に会心の一撃、15ダメージを与えた！ (x3)")]
    [TestCase("You hit the 熊 (x2) with an 矢 for 8 damage!", "矢で熊に8ダメージを与えた！ (x2)")]
    [TestCase("You hit the 熊 with a 矢 for 5 damage.", "矢で熊に5ダメージを与えた")]
    [TestCase("You critically hit the 熊 (x3) with a 短弓!", "短弓で熊に会心の一撃！ (x3)")]
    [TestCase("You hit the 熊 (x2) with a 矢!", "矢で熊に命中した (x2)")]
    [TestCase("You hit the 熊 with a 矢, but your mental attack has no effect.", "矢で熊に命中したが、精神攻撃は効果がない")]
    [TestCase("You critically hit the 熊 with a 矢.", "矢で熊に会心の一撃！")]
    [TestCase("You hit the 熊 with a 矢.", "矢で熊に命中した")]
    // Color-wrapped
    [TestCase("{{W|You critically hit the 熊 (x3) with a 短弓 for 15 damage!}}", "{{W|短弓で熊に会心の一撃、15ダメージを与えた！ (x3)}}")]
    public void Translate_MissileHitPlayerAttacker(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Hit Something/Something Hits Family ---

    [TestCase("Something hits the 熊 with an 矢 for 5 damage.", "何かが矢で熊に5ダメージを与えた")]
    [TestCase("Something hits the 熊 with an 矢.", "何かが矢で熊に命中した")]
    [TestCase("You hit something to the north!", "to the northの方角の何かに命中した")]
    [TestCase("The 熊 hits something to the south.", "熊はto the southの方角の何かに命中させた")]
    // Color-wrapped
    [TestCase("{{r|Something hits the 熊 with an 矢 for 5 damage.}}", "{{r|何かが矢で熊に5ダメージを与えた}}")]
    public void Translate_MissileHitSomethingFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Hit (3P attacker hits player) ---

    [TestCase("The 熊 critically hits you (x3) with an 矢 for 10 damage!", "熊の矢が会心し、10ダメージを受けた！ (x3)")]
    [TestCase("The 熊 hits you (x2) with a 矢 for 8 damage!", "熊の矢で8ダメージを受けた！ (x2)")]
    [TestCase("The 熊 critically hits you with an 矢 for 5 damage.", "熊の矢が会心し、5ダメージを受けた")]
    [TestCase("The 熊 hits you with a 矢 for 5 damage.", "熊の矢で5ダメージを受けた")]
    [TestCase("The 熊 critically hits you with an 矢!", "熊の矢で会心の一撃を受けた")]
    [TestCase("The 熊 hits you with a 矢.", "熊の矢で攻撃を受けた")]
    [TestCase("The 熊 critically hits you! (x2)", "熊の会心の一撃を受けた！ (x2)")]
    [TestCase("The 熊 hits you! (x3)", "熊の攻撃を受けた！ (x3)")]
    // Color-wrapped
    [TestCase("{{r|The 熊 hits you (x2) with a 矢 for 8 damage!}}", "{{r|熊の矢で8ダメージを受けた！ (x2)}}")]
    public void Translate_MissileHit3PAttacksPlayer(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Hit (3P attacker hits 3P defender) ---

    [TestCase("The 熊 critically hits the スナップジョー (x2) with an 矢 for 8 damage!", "熊は矢でスナップジョーに会心の一撃、8ダメージを与えた！ (x2)")]
    [TestCase("The 熊 hits the スナップジョー (x3) with a 矢 for 10 damage!", "熊は矢でスナップジョーに10ダメージを与えた！ (x3)")]
    [TestCase("The 熊 critically hits the スナップジョー with an 矢!", "熊は矢でスナップジョーに会心の一撃")]
    [TestCase("The 熊 hits the スナップジョー with a 矢.", "熊は矢でスナップジョーに命中した")]
    [TestCase("The 熊 hits the スナップジョー! (x3)", "熊はスナップジョーに命中した (x3)")]
    [TestCase("The 熊 critically hits the スナップジョー! (x2)", "熊はスナップジョーに会心の一撃！ (x2)")]
    // Color-wrapped
    [TestCase("{{W|The 熊 hits the スナップジョー (x3) with a 矢 for 10 damage!}}", "{{W|熊は矢でスナップジョーに10ダメージを与えた！ (x3)}}")]
    public void Translate_MissileHit3PAttacks3P(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Missile Fail to Penetrate Family ---

    [TestCase("The 熊's 矢 fails to penetrate the スナップジョー's armor [5]!", "熊の矢はスナップジョーの装甲を貫けなかった！ [5]")]
    [TestCase("The 熊's 矢 fails to penetrate the スナップジョー's armor!", "熊の矢はスナップジョーの装甲を貫けなかった！")]
    [TestCase("The 熊 fails to penetrate the スナップジョー's armor [3]!", "熊はスナップジョーの装甲を貫けなかった！ [3]")]
    [TestCase("The 熊's 矢 fails to penetrate your armor [3]!", "熊の矢はあなたの装甲を貫けなかった！ [3]")]
    [TestCase("The 熊's 矢 fails to penetrate your armor!", "熊の矢はあなたの装甲を貫けなかった！")]
    [TestCase("The 熊 fails to penetrate your armor!", "熊はあなたの装甲を貫けなかった！")]
    // Color-wrapped
    [TestCase("{{g|The 熊's 矢 fails to penetrate your armor [3]!}}", "{{g|熊の矢はあなたの装甲を貫けなかった！ [3]}}")]
    public void Translate_MissileFailPenetrateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Suppressive/Flattening Fire Family ---

    [TestCase("The 熊's suppressive fire locks the スナップジョー in place.", "熊の制圧射撃がスナップジョーをその場に釘付けにした")]
    [TestCase("The 熊's flattening fire drops the スナップジョー to the ground!", "熊の制圧射撃がスナップジョーを地面に叩き伏せた！")]
    // Color-wrapped
    [TestCase("{{g|The 熊's suppressive fire locks the スナップジョー in place.}}", "{{g|熊の制圧射撃がスナップジョーをその場に釘付けにした}}")]
    public void Translate_SuppressiveFireFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Wound/Disorient Family ---

    [TestCase("You wound the 熊.", "熊に深手を負わせた")]
    [TestCase("The 熊 wounds the スナップジョー.", "熊はスナップジョーに深手を負わせた")]
    [TestCase("You disorient the 熊.", "熊を混乱させた")]
    [TestCase("The 熊 disorients the スナップジョー.", "熊はスナップジョーを混乱させた")]
    // Color-wrapped
    [TestCase("{{r|You wound the 熊.}}", "{{r|熊に深手を負わせた}}")]
    public void Translate_WoundDisorientFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Wild Shot Family ---

    [TestCase("Your shot goes wild!", "あなたの弾が逸れた！")]
    [TestCase("The 熊's shot goes wild!", "熊の弾が逸れた！")]
    // Color-wrapped
    [TestCase("{{R|Your shot goes wild!}}", "{{R|あなたの弾が逸れた！}}")]
    public void Translate_WildShotFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Pass By Family ---

    [TestCase("The 矢 whizzes past to the north.", "矢がto the northのそばを通り過ぎた")]
    [TestCase("The 矢 flies past to the south.", "矢がto the southのそばを通り過ぎた")]
    // Color-wrapped
    [TestCase("{{r|The 矢 whizzes past to the north.}}", "{{r|矢がto the northのそばを通り過ぎた}}")]
    public void Translate_PassByFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Door Open/Close/Unlock Family ---

    [TestCase("You cannot open the 扉.", "扉を開けられない")]
    [TestCase("You are out of phase with the 扉.", "扉と位相がずれている")]
    [TestCase("You cannot reach the 扉.", "扉に手が届かない")]
    [TestCase("You can't unlock the 扉 from a distance.", "離れた位置から扉の鍵を開けることはできない")]
    [TestCase("You can't unlock the 扉.", "扉の鍵を開けられない")]
    [TestCase("The 鍵 unlocks the 扉.", "鍵が扉の鍵を開けた")]
    [TestCase("You interface with the 扉 and unlock it.", "扉にインターフェースで接続して鍵を開けた")]
    [TestCase("The 扉 cannot be closed.", "扉は閉められない")]
    [TestCase("You cannot close the 扉.", "扉を閉められない")]
    [TestCase("The 扉 cannot be closed with you in the way.", "あなたが邪魔で扉を閉められない")]
    [TestCase("The 扉 cannot be closed with the 熊 in the way.", "熊が邪魔で扉を閉められない")]
    [TestCase("You lay your hand upon the 扉 and draw forth its passcode. You enter the code and the 扉 unlocks.", "扉に手を当ててパスコードを読み取った。コードを入力すると扉の鍵が開いた")]
    // Color-wrapped
    [TestCase("{{r|You cannot open the 扉.}}", "{{r|扉を開けられない}}")]
    [TestCase("{{r|You can't unlock the 扉.}}", "{{r|扉の鍵を開けられない}}")]
    [TestCase("{{r|The 扉 cannot be closed.}}", "{{r|扉は閉められない}}")]
    public void Translate_DoorFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Popup Coverage Families ---

    [TestCase("You cannot reach the 熊!", "熊に手が届かない")]
    [TestCase("You have no hands to beat at the flames with!", "手がないので火を叩けない！")]
    public void Translate_FirefightingPopupFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("The security door unlocks with a loud clank and swings open.", "頑丈なドアが大きな音とともに解錠され開いた。")]
    [TestCase("The security door swings closed and locks with a loud clank.", "頑丈なドアが閉じて大きな音で施錠された。")]
    public void Translate_SecurityDoorPopupFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("Your スクラップ is too unstable to craft with.", "スクラップは工作に使うには不安定すぎる。")]
    [TestCase("You don't have the required ingredient: 銅線!", "必要な材料が足りない: 銅線！")]
    [TestCase("You don't have the required <AB> bits! You have:\n\n AB: 1", "必要な <AB> ビットが足りない！現在の所持:\n\nAB: 1")]
    public void Translate_TinkeringPopupFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("Your Strength is {{C|100}}.\n\nYou may not raise an attribute above 100.", "あなたの筋力は{{C|100}}だ。\n\n能力値は100を超えて上げられない。")]
    [TestCase("Your base Strength is {{C|18}}, modified to {{G|20}}.\n\nYou may not raise an attribute above 100.", "あなたの筋力の基本値は{{C|18}}で、修正後は{{G|20}}だ。\n\n能力値は100を超えて上げられない。")]
    [TestCase("You have increased your Strength to {{C|19}}!", "あなたの筋力が{{C|19}}になった！")]
    public void Translate_StatusScreenPopupFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("You swell with the inspiration to name an item.", "あなたはアイテムに名付けたい衝動に駆られた。")]
    [TestCase("You swell with the inspiration to name your 銅の短剣. Do you wish to?", "あなたは銅の短剣に名付けたい衝動に駆られた。そうしますか？")]
    public void Translate_ItemNamingPopupFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Nosebleed Family ---

    [TestCase("Your nose begins bleeding more heavily.", "あなたの鼻からさらに激しくbleedingが始まった")]
    [TestCase("Your nose begins bleeding.", "あなたの鼻からbleedingが始まった")]
    [TestCase("Your nose stops bleeding quite so heavily.", "あなたの鼻からのbleedingが少し治まった")]
    [TestCase("Your nose stops bleeding.", "あなたの鼻からのbleedingが止まった")]
    [TestCase("The 熊's nose begins bleeding.", "熊の鼻からbleedingが始まった")]
    [TestCase("The 熊's nose stops bleeding.", "熊の鼻からのbleedingが止まった")]
    [TestCase("The 熊's noses begin bleeding more heavily.", "熊の鼻からさらに激しくbleedingが始まった")]
    [TestCase("The 熊's noses stop bleeding quite so heavily.", "熊の鼻からのbleedingが少し治まった")]
    // Color-wrapped
    [TestCase("{{r|Your nose begins bleeding.}}", "{{r|あなたの鼻からbleedingが始まった}}")]
    public void Translate_NosebleedFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Harvest Family ---

    [TestCase("You harvest a ウィッチウッド from the 木.", "木からウィッチウッドを収穫した")]
    [TestCase("You harvest three ウィッチウッド from the 木.", "木からthree個のウィッチウッドを収穫した")]
    [TestCase("You harvest a ウィッチウッド.", "ウィッチウッドを収穫した")]
    [TestCase("There is nothing left to harvest.", "収穫できるものが残っていない")]
    [TestCase("The 熊 harvests a ウィッチウッド from the 木.", "熊は木からウィッチウッドを収穫した")]
    [TestCase("The 熊 harvests three ウィッチウッド from the 木.", "熊は木からthree ウィッチウッドを収穫した")]
    // Color-wrapped
    [TestCase("{{g|You harvest a ウィッチウッド from the 木.}}", "{{g|木からウィッチウッドを収穫した}}")]
    public void Translate_HarvestFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Rifle Through (Garbage.cs) Family ---

    [TestCase("You rifle through the ゴミ山, and find a 銅線.", "ゴミ山を漁り、銅線を見つけた")]
    [TestCase("You rifle through the ゴミ山, but you find nothing.", "ゴミ山を漁ったが、何も見つからなかった")]
    [TestCase("The 熊 rifles through the ゴミ山.", "熊はゴミ山を漁った")]
    [TestCase("Somebody rifles through the ゴミ山.", "誰かがゴミ山を漁った")]
    // Color-wrapped
    [TestCase("{{g|You rifle through the ゴミ山, and find a 銅線.}}", "{{g|ゴミ山を漁り、銅線を見つけた}}")]
    [TestCase("{{K|You rifle through the ゴミ山, but you find nothing.}}", "{{K|ゴミ山を漁ったが、何も見つからなかった}}")]
    public void Translate_RifleThroughFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Butcher Cybernetic Family ---

    [TestCase("You butcher a サイバネティック from the 死体.", "死体からサイバネティックを解体した")]
    [TestCase("The 熊 butchers a サイバネティック from the 死体.", "熊は死体からサイバネティックを解体した")]
    [TestCase("You rip a サイバネティック out of the 死体, but destroy it in the process.", "死体からサイバネティックを引き抜いたが、その過程で壊してしまった")]
    [TestCase("The 熊 rips a サイバネティック out of the 死体, but destroys it in the process.", "熊は死体からサイバネティックを引き抜いたが、その過程で壊してしまった")]
    // Color-wrapped
    [TestCase("{{g|You butcher a サイバネティック from the 死体.}}", "{{g|死体からサイバネティックを解体した}}")]
    [TestCase("{{r|You rip a サイバネティック out of the 死体, but destroy it in the process.}}", "{{r|死体からサイバネティックを引き抜いたが、その過程で壊してしまった}}")]
    public void Translate_ButcherCyberneticFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Sunder Mind (nose/core/brain) Family ---

    [TestCase("The 熊's nose begins to bleed.", "熊の鼻から血が流れ始めた")]
    [TestCase("Your nose begins to bleed.", "あなたの鼻から血が流れ始めた")]
    [TestCase("The ロボット's core begins to leak.", "ロボットのコアから液漏れが始まった")]
    [TestCase("Your core begins to leak.", "あなたのコアから液漏れが始まった")]
    [TestCase("The 熊's brain begins to hemorrhage.", "熊の脳から出血が始まった")]
    [TestCase("Your brain begins to hemorrhage.", "あなたの脳から出血が始まった")]
    // Color-wrapped
    [TestCase("{{r|The 熊's nose begins to bleed.}}", "{{r|熊の鼻から血が流れ始めた}}")]
    public void Translate_SunderMindFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Combat Reach/Phase Family ---

    [TestCase("The 熊 cannot reach the スナップジョー.", "熊はスナップジョーに届かない")]
    [TestCase("The 熊's attack passes through the スナップジョー!", "熊の攻撃はスナップジョーをすり抜けた！")]
    [TestCase("Your attack passes through the スナップジョー!", "あなたの攻撃はスナップジョーをすり抜けた！")]
    // Color-wrapped
    [TestCase("{{r|The 熊 cannot reach the スナップジョー.}}", "{{r|熊はスナップジョーに届かない}}")]
    [TestCase("{{r|The 熊's attack passes through the スナップジョー!}}", "{{r|熊の攻撃はスナップジョーをすり抜けた！}}")]
    public void Translate_CombatReachPhaseFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Space-Time Vortex Family ---

    [TestCase("Two space-time vortices come into contact and both explode!", "2つのspace-time vorticesが接触し、両方とも爆発した！")]
    // Color-wrapped
    [TestCase("{{R|Two space-time vortices come into contact and both explode!}}", "{{R|2つのspace-time vorticesが接触し、両方とも爆発した！}}")]
    public void Translate_SpaceTimeVortexFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Liquid Reaction Family ---

    [TestCase("The reacting liquids congeal into a スラッジ.", "反応した液体が凝固しスラッジになった")]
    [TestCase("The liquids stop reacting.", "液体の反応が止まった")]
    [TestCase("The primordial soup to the east starts reacting with the 酸.", "to the eastの原初のスープが酸と反応を始めた")]
    // Color-wrapped
    [TestCase("{{g|The reacting liquids congeal into a スラッジ.}}", "{{g|反応した液体が凝固しスラッジになった}}")]
    public void Translate_LiquidReactionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Glitch Family ---

    [TestCase("The 装置 starts to glitch.", "装置がグリッチし始めた")]
    [TestCase("The liquid mixture inside the 水筒 starts to glitch.", "水筒の中の混合液がグリッチし始めた")]
    // Color-wrapped
    [TestCase("{{W|The 装置 starts to glitch.}}", "{{W|装置がグリッチし始めた}}")]
    public void Translate_GlitchFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Warm Static Effect Family ---

    [TestCase("confused applied to 熊.", "confusedが熊に適用された")]
    [TestCase("No valid targets for confused.", "confusedの有効な対象がない")]
    // Color-wrapped
    [TestCase("{{K|confused applied to 熊.}}", "{{K|confusedが熊に適用された}}")]
    public void Translate_WarmStaticEffectFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Queasiness/Skin/Rind Family ---

    [TestCase("You feel a little queasy.", "少し吐き気がする")]
    [TestCase("The 熊 feels a little queasy.", "熊は少し吐き気がする")]
    [TestCase("The 熊's queasiness passes.", "熊の吐き気が治まった")]
    [TestCase("Your queasiness passes.", "あなたの吐き気が治まった")]
    [TestCase("The 熊's skin itches furiously.", "熊の肌が猛烈にかゆくなった")]
    [TestCase("Your skin itches furiously.", "あなたの肌が猛烈にかゆくなった")]
    [TestCase("The 熊's rind recrystallizes and hardens once more.", "熊の外皮が再結晶化し、再び硬くなった")]
    [TestCase("Your rind recrystallizes and hardens once more.", "あなたの外皮が再結晶化し、再び硬くなった")]
    [TestCase("The 熊's glow dims until it's extinguished.", "熊の輝きが消えるまで薄れた")]
    [TestCase("Your glow dims until it's extinguished.", "あなたの輝きが消えるまで薄れた")]
    // Color-wrapped
    [TestCase("{{r|The 熊's queasiness passes.}}", "{{r|熊の吐き気が治まった}}")]
    [TestCase("{{g|The 熊's glow dims until it's extinguished.}}", "{{g|熊の輝きが消えるまで薄れた}}")]
    public void Translate_BodyEffectFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Force Bubble Family ---

    [TestCase("A force bubble pops into being around the 変異者.", "変異者の周りにフォースバブルが出現した")]
    // Color-wrapped
    [TestCase("{{B|A force bubble pops into being around the 変異者.}}", "{{B|変異者の周りにフォースバブルが出現した}}")]
    public void Translate_ForceBubbleFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Burns Into Slag Family ---

    [TestCase("The 電池 burns into bright slag.", "電池は燃えて明るいスラグになった")]
    // Color-wrapped
    [TestCase("{{R|The 電池 burns into bright slag.}}", "{{R|電池は燃えて明るいスラグになった}}")]
    public void Translate_BurnsIntoSlagFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Flailing/Battering Family ---

    [TestCase("Suddenly the 武器 starts flailing around and battering you!", "突然武器が暴れ出し、youを殴りつけた！")]
    [TestCase("Two tubular sections of the 武器 flail around and batter you!", "武器の2本の管状の部分が暴れ、youを殴りつけた！")]
    // Color-wrapped
    [TestCase("{{r|Two tubular sections of the 武器 flail around and batter you!}}", "{{r|武器の2本の管状の部分が暴れ、youを殴りつけた！}}")]
    public void Translate_FlailingBatteringFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Collide Family ---

    [TestCase("The ディスク and the 円盤 collide and fall to the ground.", "ディスクと円盤がぶつかり合い、地面に落ちた")]
    // Color-wrapped
    [TestCase("{{r|The ディスク and the 円盤 collide and fall to the ground.}}", "{{r|ディスクと円盤がぶつかり合い、地面に落ちた}}")]
    public void Translate_CollideFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Ammo/Reload Family ---

    [TestCase("You have no more ammo for the 銃.", "銃の弾薬がもうない")]
    [TestCase("You reload the 銃 with a マガジン.", "銃にマガジンを装填した")]
    // Color-wrapped
    [TestCase("{{r|You have no more ammo for the 銃.}}", "{{r|銃の弾薬がもうない}}")]
    public void Translate_AmmoReloadFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Jet of Flame Family ---

    [TestCase("A jet of flame shoots out of your ロケットスケート!", "ロケットスケートから炎の噴流が噴き出した！")]
    // Color-wrapped
    [TestCase("{{R|A jet of flame shoots out of your ロケットスケート!}}", "{{R|ロケットスケートから炎の噴流が噴き出した！}}")]
    public void Translate_JetOfFlameFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Catalyze Family ---

    [TestCase("The 酸 catalyzes スラッジ into a 新スラッジ.", "酸がスラッジを触媒して新スラッジに変化させた")]
    // Color-wrapped
    [TestCase("{{g|The 酸 catalyzes スラッジ into a 新スラッジ.}}", "{{g|酸がスラッジを触媒して新スラッジに変化させた}}")]
    public void Translate_CatalyzeFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Infiltrate Family ---

    [TestCase("You infiltrate the メカ!", "メカに潜入した！")]
    [TestCase("The 熊 infiltrates the メカ!", "熊はメカに潜入した！")]
    // Color-wrapped
    [TestCase("{{g|You infiltrate the メカ!}}", "{{g|メカに潜入した！}}")]
    public void Translate_InfiltrateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Grow Body Part Family ---

    [TestCase("A 腕 grows out of your 胴体!", "腕があなたの胴体から生えてきた！")]
    // Color-wrapped
    [TestCase("{{g|A 腕 grows out of your 胴体!}}", "{{g|腕があなたの胴体から生えてきた！}}")]
    public void Translate_GrowBodyPartFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Deploy Family ---

    [TestCase("The 技師 deploys a タレット.", "技師はタレットを展開した")]
    [TestCase("The 技師 sets up a ベッドロール.", "技師はベッドロールを設置した")]
    // Color-wrapped
    [TestCase("{{g|The 技師 deploys a タレット.}}", "{{g|技師はタレットを展開した}}")]
    public void Translate_DeployFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- E-Ros Teleportation Family ---

    [TestCase("E-Ros yells, 'I'm coming, リーダー!'", "E-Rosは「今行くよ、リーダー！」と叫んだ")]
    // Actual game format: {{W|...}} wraps inner quote only (ErosTeleportation.cs:139)
    [TestCase("E-Ros yells, {{W|'I'm coming, リーダー!'}}", "E-Rosは{{W|「今行くよ、リーダー！」}}と叫んだ")]
    public void Translate_ErosTeleportationFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Eat/Apply (Tonic 3P) Family ---

    [TestCase("The 熊 eats a トニック.", "熊はトニックを食べた")]
    [TestCase("The 熊 applies a トニック.", "熊はトニックを使用した")]
    // Color-wrapped
    [TestCase("{{g|The 熊 eats a トニック.}}", "{{g|熊はトニックを食べた}}")]
    public void Translate_EatApplyTonicFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Cherubim Protection Family ---

    [TestCase("The protective force of the cherubim prevents the 冒険者 from taking anything from the reliquary.", "ケルビムの守護の力が冒険者を聖遺物櫃から何かを取ることを阻んでいる")]
    // Color-wrapped
    [TestCase("{{r|The protective force of the cherubim prevents the 冒険者 from taking anything from the reliquary.}}", "{{r|ケルビムの守護の力が冒険者を聖遺物櫃から何かを取ることを阻んでいる}}")]
    public void Translate_CherubimFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Prism Reflection Family ---

    [TestCase("As the prism shatters, a reflection of the 熊 is caught on the limen of realities and appears out of nowhere.", "プリズムが砕けると、熊の反射像が現実の狭間に捕らわれ、どこからともなく現れた")]
    [TestCase("As the prism shatters, reflections of the 熊 are caught on the limen of realities and appear out of nowhere.", "プリズムが砕けると、熊の反射像が現実の狭間に捕らわれ、どこからともなく現れた")]
    [TestCase("{{g|As the prism shatters, a reflection of the 熊 is caught on the limen of realities and appears out of nowhere.}}", "{{g|プリズムが砕けると、熊の反射像が現実の狭間に捕らわれ、どこからともなく現れた}}")]
    public void Translate_PrismReflectionFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("As the prism shatters, a reflection from the 熊 appears out of nowhere.", "As the prism shatters, a reflection from the 熊 appears out of nowhere.")]
    [TestCase("\u0001As the prism shatters, a reflection of the 熊 is caught on the limen of realities and appears out of nowhere.", "\u0001As the prism shatters, a reflection of the 熊 is caught on the limen of realities and appears out of nowhere.")]
    [TestCase("", "")]
    public void Translate_PrismReflectionFamily_FallbackAndEdgeCases(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Holographic Visage Family ---

    [TestCase("In a glissade of light, the 熊's visage morphs into an image pleasing to the Barathrumites.", "光が滑るように走り、熊の顔貌はthe Barathrumitesに好ましい姿へと変化した")]
    [TestCase("{{g|In a glissade of light, the 熊's visage morphs into an image pleasing to the Barathrumites.}}", "{{g|光が滑るように走り、熊の顔貌はthe Barathrumitesに好ましい姿へと変化した}}")]
    public void Translate_HolographicVisageFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    [TestCase("In a glissade of light, the 熊's visage morphs.", "In a glissade of light, the 熊's visage morphs.")]
    [TestCase("\u0001In a glissade of light, the 熊's visage morphs into an image pleasing to the Barathrumites.", "\u0001In a glissade of light, the 熊's visage morphs into an image pleasing to the Barathrumites.")]
    [TestCase("", "")]
    public void Translate_HolographicVisageFamily_FallbackAndEdgeCases(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Irritable Genome Family ---

    [TestCase("The 変異者's irritable genome acts up.", "変異者の過敏ゲノムが暴走した")]
    [TestCase("Your irritable genome acts up.", "あなたの過敏ゲノムが暴走した")]
    // Color-wrapped
    [TestCase("{{r|Your irritable genome acts up.}}", "{{r|あなたの過敏ゲノムが暴走した}}")]
    public void Translate_IrritableGenomeFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Ill Family ---

    [TestCase("The poison begins to abate, but you still feel nauseous.", "毒は収まり始めたが、まだ吐き気がする。")]
    [TestCase("You feel shaken and infirm.", "あなたはふらつき、衰弱を感じる。")]
    [TestCase("You feel nauseous.", "吐き気がする。")]
    [TestCase("{{g|You feel nauseous.}}", "{{g|吐き気がする。}}")]
    [TestCase("", "")]
    [TestCase("\u0001毒は収まり始めたが、まだ吐き気がする。", "\u0001毒は収まり始めたが、まだ吐き気がする。")]
    [TestCase("Some unrelated English text.", "Some unrelated English text.")]
    public void Translate_IllFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Sheva Launch Countdown Family ---

    [TestCase("Launching in 25...", "発射まで25…")]
    [TestCase("{{R|Launching in 50...}}", "{{R|発射まで50…}}")]
    [TestCase("", "")]
    [TestCase("\u0001発射まで25…", "\u0001発射まで25…")]
    [TestCase("Not a launch message", "Not a launch message")]
    public void Translate_ShevaLaunchCountdownFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Sleep Mode (Player) Family ---

    [TestCase("You enter sleep mode.", "あなたはスリープモードに入った")]
    // Color-wrapped
    [TestCase("{{C|You enter sleep mode.}}", "{{C|あなたはスリープモードに入った}}")]
    public void Translate_SleepModePlayerFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Edge cases (required for all families) ---

    [Test]
    public void Translate_ReturnsOriginal_WhenNoPatternMatches()
    {
        // Unrecognized message should pass through unchanged
        var input = "The bear performs an unknowable act.";
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(input));
    }

    [Test]
    public void Translate_ReturnsEmpty_WhenInputIsEmpty()
    {
        var result = MessagePatternTranslator.Translate(string.Empty);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_ReturnsEmpty_WhenInputIsNull()
    {
        var result = MessagePatternTranslator.Translate(null);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Translate_SkipsTranslation_WhenDirectTranslationMarkerPresent()
    {
        // \x01 marker means already translated — should pass through unchanged
        var input = "\u0001熊は疲弊した！";
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(input));
    }

    private static void AssertTranslated(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        if (string.Equals(result, input, StringComparison.Ordinal)
            && DoesVerbRouteTranslator.TryTranslatePlainSentenceForTests(input, out var routed))
        {
            result = routed;
        }

        Assert.That(result, Is.EqualTo(expected));
    }

    private static void WriteCombinedLeafFile(string destinationPath, params string[] sourcePaths)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sourcePath in sourcePaths)
        {
            if (!File.Exists(sourcePath))
            {
                TestContext.WriteLine($"Warning: Leaf source file not found: {sourcePath}");
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(sourcePath));
            foreach (var element in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var key = element.GetProperty("key").GetString();
                var text = element.GetProperty("text").GetString();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(text))
                {
                    entries[key] = text;
                }
            }
        }

        using var stream = File.Create(destinationPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WritePropertyName("entries");
        writer.WriteStartArray();
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("key", entry.Key);
            writer.WriteString("text", entry.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

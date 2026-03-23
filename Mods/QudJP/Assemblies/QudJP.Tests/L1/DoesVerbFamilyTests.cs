namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbFamilyTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
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

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    // --- Status Predicate Family ---

    // Plain text (runtime-like already-localized display names)
    [TestCase("The 熊 is exhausted!", "熊は疲弊した！")]
    [TestCase("The スナップジョー is stunned!", "スナップジョーは気絶した！")]
    [TestCase("The 熊 is stuck.", "熊は動けなくなった。")]
    [TestCase("The グロウパッド is sealed.", "グロウパッドは封印された。")]
    [TestCase("You are exhausted!", "あなたは疲弊した！")]
    // Color-wrapped (AddPlayerMessage wraps entire message in {{color|...}})
    [TestCase("{{g|The 熊 is exhausted!}}", "{{g|熊は疲弊した！}}")]
    [TestCase("{{R|The スナップジョー is stunned!}}", "{{R|スナップジョーは気絶した！}}")]
    public void Translate_StatusPredicateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Negation/Lack Family ---

    // Plain text
    [TestCase("The タレット can't hear you!", "タレットにはあなたの声が聞こえない！")]
    [TestCase("The 熊 doesn't have a consciousness to appeal to.", "熊には訴えるべき意識がない。")]
    [TestCase("You don't penetrate the スナップジョー's armor!", "スナップジョーの防具を貫通できなかった！")]
    [TestCase("You can't see!", "視界がない！")]
    [TestCase("The タレット doesn't have enough charge to fire.", "タレットはfireするのに十分なchargeがない。")]
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
        Assert.That(result, Is.EqualTo(expected));
    }
}

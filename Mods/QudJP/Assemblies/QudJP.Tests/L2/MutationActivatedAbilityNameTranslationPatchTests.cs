using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationActivatedAbilityNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Mutate_TranslatesMultipleRegisteredMutationAbilityNames_WhenPatched()
    {
        WithPatchedOwner(() =>
        {
            var mutation = new DummyMutationAbilityProvider(
                "Boost Strength",
                "Boost Agility",
                "Boost Toughness");

            mutation.Mutate(new object(), 1);

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo("筋力強化"));
                Assert.That(mutation.AgilityEntry.DisplayName, Is.EqualTo("敏捷強化"));
                Assert.That(mutation.ToughnessEntry.DisplayName, Is.EqualTo("頑健強化"));
                Assert.That(HitCount(), Is.EqualTo(3));
            });
        });
    }

    [TestCase("Excavate up", "上階へ掘削")]
    [TestCase("Discharge", "放電")]
    [TestCase("Lase", "レーザー照射")]
    [TestCase("Precognition - Start vision", "予知 - 予知視開始")]
    [TestCase("Spew", "吐き出す")]
    [TestCase("Beguile Creature", "クリーチャーを魅了")]
    [TestCase("Spit Acid", "酸吐き")]
    [TestCase("Release Adrenaline", "アドレナリン放出")]
    [TestCase("Burgeoning", "繁茂")]
    [TestCase("Burrow", "穴掘り")]
    [TestCase("Tighten 甲殻", "甲殻を締め付ける")]
    [TestCase("Clairvoyance", "千里眼")]
    [TestCase("Confusion", "混乱")]
    [TestCase("Decarbonize", "脱炭素化")]
    [TestCase("Scintillate", "きらめく")]
    [TestCase("Dominate Creature", "支配")]
    [TestCase("Emit Pulse", "パルス放出")]
    [TestCase("Teleport", "テレポート")]
    [TestCase("Force Wall", "力場壁")]
    [TestCase("Freezing Ultraray", "凍結超光線")]
    [TestCase("Knit Frosty Webs", "氷結糸を編む")]
    [TestCase("Infiltrate", "潜入")]
    [TestCase("Irisdual Beam", "アイリスデュアル光線")]
    [TestCase("Kindle", "着火")]
    [TestCase("Ley Shift", "レイシフト")]
    [TestCase("Syphon Vim", "活力吸収")]
    [TestCase("Spit Liquid", "液体吐き")]
    [TestCase("Tap the Mass Mind", "集合精神に接続")]
    [TestCase("Mental Mirror", "精神鏡")]
    [TestCase("End Metamorphosis", "変容を終える")]
    [TestCase("Metamorphosis", "変容")]
    [TestCase("Phase", "フェイズ化")]
    [TestCase("Serenity", "安らぎ")]
    [TestCase("Spacetime Vortex", "時空渦")]
    [TestCase("Spin Webs", "網を張る")]
    [TestCase("Tongue", "粘着舌")]
    [TestCase("Sting", "刺突")]
    [TestCase("Stunning Force", "衝撃念力")]
    [TestCase("Sunder Mind", "精神断裂")]
    [TestCase("Teleport Other", "他者転送")]
    [TestCase("Time Dilation", "時間延伸")]
    [TestCase("Waveform Dash", "ウェーブフォーム・ダッシュ")]
    [TestCase("Chill", "冷却")]
    [TestCase("Disintegration", "分解")]
    [TestCase("Fear Aura", "恐怖のオーラ")]
    [TestCase("Flaming Ray", "炎線")]
    [TestCase("Force Bubble", "力場泡")]
    [TestCase("Freezing Ray", "凍結線")]
    [TestCase("Magnetic Pulse", "磁気パルス")]
    [TestCase("Toast", "加熱")]
    [TestCase("Repelling Force", "反発力")]
    [TestCase("Spit Slime", "粘液吐き")]
    [TestCase("Telepathy", "テレパシー")]
    [TestCase("Teleport", "テレポート")]
    [TestCase("Belch Urchins", "ウニを吐く")]
    [TestCase("Breathe Fire", "火炎ブレス")]
    [TestCase("Breathe Ice", "氷結ブレス")]
    [TestCase("Breathe Normality Gas", "正常化ブレス")]
    [TestCase("Breathe Corrosive Gas", "腐食ブレス")]
    [TestCase("Breathe Confusion Gas", "混乱ブレス")]
    [TestCase("Breathe Stun Gas", "朦朧ブレス")]
    [TestCase("Breathe Poison Gas", "毒ブレス")]
    [TestCase("Breathe Sleep Gas", "睡眠ブレス")]
    [TestCase("Breathe Shame Gas", "恥辱ブレス")]
    [TestCase("Release Corrosive Gas", "腐食性ガス放出")]
    [TestCase("Release Sleep Gas", "睡眠ガス放出")]
    [TestCase("Release Poison Gas", "毒ガス放出")]
    [TestCase("Release Confusion Gas", "混乱ガス放出")]
    [TestCase("Release Normality Gas", "正常化ガス放出")]
    [TestCase("Release Defoliant", "落葉剤放出")]
    [TestCase("Release Fungicide", "殺真菌剤放出")]
    [TestCase("Release Glitter Dust", "グリッターダスト放出")]
    [TestCase("Release Plasma", "プラズマ放出")]
    [TestCase("Crungling Gaze", "クラングリングの視線")]
    [TestCase("Lithifying Gaze", "石化の視線")]
    [TestCase("Quill Fling", "棘毛投げ")]
    [TestCase("Temporal Fugue", "時間遁走")]
    [TestCase("Quantum Fugue", "量子フーガ")]
    public void Mutate_TranslatesSingleMutationAbilityName_WhenPatched(string source, string expected)
    {
        WithPatchedOwner(() =>
        {
            var mutation = new DummyMutationAbilityProvider(source);

            mutation.Mutate(new object(), 1);

            Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo(expected));
        });
    }

    [Test]
    public void Mutate_LeavesUnknownMutationAbilityNameUnchanged_WhenPatched()
    {
        WithPatchedOwner(() =>
        {
            var mutation = new DummyMutationAbilityProvider("Unknown Mutation Ability");

            mutation.Mutate(new object(), 1);

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo("Unknown Mutation Ability"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [TestCase("", "")]
    [TestCase("\u0001Boost Strength", "Boost Strength")]
    public void Mutate_PreservesEmptyAndDirectMarkedActivationNames_WhenPatched(string source, string expected)
    {
        WithPatchedOwner(() =>
        {
            var mutation = new DummyMutationAbilityProvider(source);

            mutation.Mutate(new object(), 1);

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Mutate_TranslatesColorTaggedActivationName_WhenPatched()
    {
        WithPatchedOwner(() =>
        {
            var mutation = new DummyMutationAbilityProvider("{{C|Boost Strength}}");

            mutation.Mutate(new object(), 1);

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo("{{C|筋力強化}}"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void SyncAbilityName_TranslatesUpdatedLightManipulationAbilityName_WhenPatched()
    {
        WithPatchedSyncAbilityName(() =>
        {
            var mutation = new DummyMutationAbilityProvider("Lase (4 charges)");

            mutation.SyncAbilityName();

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo("レーザー照射 (4チャージ)"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void SyncAbilityName_StripsDirectMarkerFromRecoilZone_WhenPatched()
    {
        WithPatchedSyncAbilityName(() =>
        {
            var mutation = new DummyMutationAbilityProvider("Recoil to \u0001ジョッパ");

            mutation.SyncAbilityName();

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo("ジョッパへ帰還"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [TestCase("", "", 0)]
    [TestCase("\u0001Lase (4 charges)", "Lase (4 charges)", 0)]
    [TestCase("<color=red>Lase (4 charges)</color>", "<color=red>レーザー照射 (4チャージ)</color>", 1)]
    [TestCase("\u0001<color=red>Lase (4 charges)</color>", "<color=red>Lase (4 charges)</color>", 0)]
    [TestCase("Unknown Ability (3 charges)", "Unknown Ability (3 charges)", 0)]
    public void SyncAbilityName_HandlesFallbackEmptyAndDirectMarkedAbilityNames_WhenPatched(
        string source,
        string expected,
        int expectedHitCount)
    {
        WithPatchedSyncAbilityName(() =>
        {
            var mutation = new DummyMutationAbilityProvider(source);

            mutation.SyncAbilityName();

            Assert.Multiple(() =>
            {
                Assert.That(mutation.StrengthEntry.DisplayName, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(expectedHitCount));
            });
        });
    }


    [Test]
    public void RegistrationNameFallbackSetter_RejectsNonStringDisplayNameMember()
    {
        var entry = new DummyMutationActivatedAbilityEntryWithObjectDisplayName();
        var method = typeof(ActivatedAbilityRegistrationNameTranslation).GetMethod(
            "SetStringMemberValue",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        var result = method!.Invoke(null, new object[] { entry, "DisplayName", "筋力強化" });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(false));
            Assert.That(entry.DisplayName, Is.EqualTo("Boost Strength"));
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = "qudjp.tests.mutation-activated-ability-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationAbilityProvider), nameof(DummyMutationAbilityProvider.Mutate), typeof(object), typeof(int)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(MutationActivatedAbilityNameTranslationPatch),
                    nameof(MutationActivatedAbilityNameTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedSyncAbilityName(Action action)
    {
        var harmonyId = "qudjp.tests.mutation-activated-ability-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMutationAbilityProvider), nameof(DummyMutationAbilityProvider.SyncAbilityName)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(MutationActivatedAbilityNameTranslationPatch),
                    nameof(MutationActivatedAbilityNameTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            MutationActivatedAbilityNameTranslationPatch.Context,
            MutationActivatedAbilityNameTranslationPatch.Family);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string GetRepositoryDictionaryDirectory()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries"));
    }
}

internal sealed class DummyMutationAbilityProvider
{
    private readonly string _strengthName;
    private readonly string? _agilityName;
    private readonly string? _toughnessName;

    public DummyMutationAbilityProvider(string strengthName, string? agilityName = null, string? toughnessName = null)
    {
        _strengthName = strengthName;
        _agilityName = agilityName;
        _toughnessName = toughnessName;
        StrengthEntry = new DummyMutationActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = strengthName };
        AgilityEntry = new DummyMutationActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = agilityName ?? string.Empty };
        ToughnessEntry = new DummyMutationActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = toughnessName ?? string.Empty };
    }

    public Guid StrengthActivatedAbilityID { get; private set; }

    public Guid AgilityActivatedAbilityID { get; private set; }

    public Guid ToughnessActivatedAbilityID { get; private set; }

    public DummyMutationActivatedAbilityEntry StrengthEntry { get; }

    public DummyMutationActivatedAbilityEntry AgilityEntry { get; }

    public DummyMutationActivatedAbilityEntry ToughnessEntry { get; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Mutate(object gameObject, int level)
    {
        _ = gameObject;
        _ = level;
        StrengthActivatedAbilityID = StrengthEntry.ID;
        StrengthEntry.DisplayName = _strengthName;
        if (_agilityName is not null)
        {
            AgilityActivatedAbilityID = AgilityEntry.ID;
            AgilityEntry.DisplayName = _agilityName;
        }
        if (_toughnessName is not null)
        {
            ToughnessActivatedAbilityID = ToughnessEntry.ID;
            ToughnessEntry.DisplayName = _toughnessName;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncAbilityName()
    {
        StrengthActivatedAbilityID = StrengthEntry.ID;
        StrengthEntry.DisplayName = _strengthName;
    }

    public DummyMutationActivatedAbilityEntry? MyActivatedAbility(Guid id)
    {
        if (id == StrengthEntry.ID)
        {
            return StrengthEntry;
        }
        if (id == AgilityEntry.ID)
        {
            return AgilityEntry;
        }
        if (id == ToughnessEntry.ID)
        {
            return ToughnessEntry;
        }

        return null;
    }

    public bool SetMyActivatedAbilityDisplayName(Guid id, string displayName)
    {
        var entry = MyActivatedAbility(id);
        if (entry is null)
        {
            return false;
        }

        entry.DisplayName = displayName;
        return true;
    }
}

internal sealed class DummyMutationActivatedAbilityEntry
{
    public Guid ID { get; init; }

    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class DummyMutationActivatedAbilityEntryWithObjectDisplayName
{
    public object DisplayName = "Boost Strength";
}

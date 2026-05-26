using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SkillActivatedAbilityNameTranslationPatchTests
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
    public void AddSkill_TranslatesMultipleRegisteredSkillAbilityNames_WhenPatched()
    {
        WithPatchedOwner(() =>
        {
            var skill = new DummySkillAbilityProvider("Lay Mine", "Set Bomb");

            skill.AddSkill(new object());

            Assert.Multiple(() =>
            {
                Assert.That(skill.PrimaryEntry.DisplayName, Is.EqualTo("地雷設置"));
                Assert.That(skill.SecondaryEntry.DisplayName, Is.EqualTo("爆弾設置"));
                Assert.That(HitCount(), Is.EqualTo(2));
            });
        });
    }

    [TestCase("Empty the Clips", "全弾発射")]
    [TestCase("Recharge", "充電")]
    [TestCase("Dismember", "切断")]
    [TestCase("Hook and Drag", "フック・アンド・ドラッグ")]
    [TestCase("Harvest Plants", "収穫")]
    [TestCase("Dueling Stance", "決闘スタンス")]
    [TestCase("Rebuke Robot", "ロボットを叱責")]
    [TestCase("Shank", "シャンク")]
    [TestCase("Berserk!", "狂戦！")]
    [TestCase("Butcher Corpses", "死体を解体")]
    [TestCase("Slam", "叩きつけ")]
    [TestCase("Demolish", "破壊")]
    [TestCase("Meditate", "瞑想")]
    [TestCase("En Garde!", "アン・ガルド！")]
    [TestCase("Lunge", "ランジ")]
    [TestCase("Swipe", "薙ぎ")]
    [TestCase("Flurry", "連撃")]
    [TestCase("Proselytize", "布教")]
    [TestCase("Amputate Limb", "四肢切断")]
    [TestCase("Hobble", "足止め")]
    [TestCase("Make Camp", "野営")]
    [TestCase("Deploy Turret", "タレット展開")]
    [TestCase("Catapult", "カタパルト")]
    [TestCase("Howl", "遠吠え")]
    [TestCase("Submerge", "潜る")]
    [TestCase("Conk", "コンク")]
    [TestCase("Sweep", "掃射")]
    [TestCase("Berate", "罵倒")]
    [TestCase("Intimidate", "威圧")]
    [TestCase("Mark Target", "目標をマーク")]
    [TestCase("Shield Wall", "シールドウォール")]
    [TestCase("Shield Slam", "シールドスラム")]
    [TestCase("Charge", "突進")]
    [TestCase("Death From Above", "デス・フロム・アバブ")]
    [TestCase("Juke", "フェイント")]
    public void AddSkill_TranslatesSingleRegisteredSkillAbilityName_WhenPatched(string source, string expected)
    {
        WithPatchedOwner(() =>
        {
            var skill = new DummySkillAbilityProvider(source);

            skill.AddSkill(new object());

            Assert.That(skill.PrimaryEntry.DisplayName, Is.EqualTo(expected));
        });
    }

    [TestCase("Decapitate", "斬首")]
    [TestCase("Akimbo", "二挺拳銃")]
    [TestCase("Rejoinder", "反撃")]
    public void AddAbility_TranslatesNoArgumentSkillAbilityName_WhenPatched(string source, string expected)
    {
        var harmonyId = "qudjp.tests.skill-activated-ability-addability." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillAbilityProvider), nameof(DummySkillAbilityProvider.AddAbility)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SkillActivatedAbilityNameTranslationPatch),
                    nameof(SkillActivatedAbilityNameTranslationPatch.Postfix),
                    typeof(object))));

            var skill = new DummySkillAbilityProvider(source);

            skill.AddAbility();

            Assert.Multiple(() =>
            {
                Assert.That(skill.PrimaryEntry.DisplayName, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void SyncAbility_TranslatesRegisteredSkillAbilityName_WhenPatched()
    {
        var harmonyId = "qudjp.tests.skill-activated-ability-sync." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillAbilityProvider), nameof(DummySkillAbilityProvider.SyncAbility), typeof(bool)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SkillActivatedAbilityNameTranslationPatch),
                    nameof(SkillActivatedAbilityNameTranslationPatch.Postfix),
                    typeof(object))));

            var skill = new DummySkillAbilityProvider("Jump");

            skill.SyncAbility(false);

            Assert.Multiple(() =>
            {
                Assert.That(skill.PrimaryEntry.DisplayName, Is.EqualTo("ジャンプ"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void AddSkill_LeavesUnknownRegisteredSkillAbilityNameUnchanged_WhenPatched()
    {
        WithPatchedOwner(() =>
        {
            var skill = new DummySkillAbilityProvider("Unknown Skill Ability");

            skill.AddSkill(new object());

            Assert.Multiple(() =>
            {
                Assert.That(skill.PrimaryEntry.DisplayName, Is.EqualTo("Unknown Skill Ability"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = "qudjp.tests.skill-activated-ability-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySkillAbilityProvider), nameof(DummySkillAbilityProvider.AddSkill), typeof(object)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(SkillActivatedAbilityNameTranslationPatch),
                    nameof(SkillActivatedAbilityNameTranslationPatch.Postfix),
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
            SkillActivatedAbilityNameTranslationPatch.Context,
            SkillActivatedAbilityNameTranslationPatch.Family);
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

internal sealed class DummySkillAbilityProvider
{
    private readonly string _primaryName;
    private readonly string? _secondaryName;

    public DummySkillAbilityProvider(string primaryName, string? secondaryName = null)
    {
        _primaryName = primaryName;
        _secondaryName = secondaryName;
        PrimaryEntry = new DummySkillActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = primaryName };
        SecondaryEntry = new DummySkillActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = secondaryName ?? string.Empty };
    }

    public Guid ActivatedAbilityID { get; private set; }

    public Guid TimedActivatedAbilityID { get; private set; }

    public DummySkillActivatedAbilityEntry PrimaryEntry { get; }

    public DummySkillActivatedAbilityEntry SecondaryEntry { get; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool AddSkill(object gameObject)
    {
        _ = gameObject;
        ActivatedAbilityID = PrimaryEntry.ID;
        PrimaryEntry.DisplayName = _primaryName;
        if (_secondaryName is not null)
        {
            TimedActivatedAbilityID = SecondaryEntry.ID;
            SecondaryEntry.DisplayName = _secondaryName;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddAbility()
    {
        ActivatedAbilityID = PrimaryEntry.ID;
        PrimaryEntry.DisplayName = _primaryName;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncAbility(bool silent)
    {
        _ = silent;
        ActivatedAbilityID = PrimaryEntry.ID;
        PrimaryEntry.DisplayName = _primaryName;
    }

    public DummySkillActivatedAbilityEntry? MyActivatedAbility(Guid id)
    {
        if (id == PrimaryEntry.ID)
        {
            return PrimaryEntry;
        }
        if (id == SecondaryEntry.ID)
        {
            return SecondaryEntry;
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

internal sealed class DummySkillActivatedAbilityEntry
{
    public Guid ID { get; init; }

    public string DisplayName { get; set; } = string.Empty;
}

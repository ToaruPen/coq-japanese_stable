using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActivatedAbilityMiscProviderTranslationPatchTests
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

    [TestCase("Clone", "クローン作成")]
    [TestCase("Dig", "掘る")]
    [TestCase("Engulf", "呑み込む")]
    [TestCase("Recoil", "帰還")]
    [TestCase("Run Over", "轢く")]
    [TestCase("Rifle through Trash", "ゴミ漁り")]
    public void Initialize_TranslatesRegisteredAbilityName_WhenPatched(string source, string expected)
    {
        WithPatchedOwner(nameof(DummyAbilityProvider.Initialize), () =>
        {
            var provider = new DummyAbilityProvider(source);

            provider.Initialize();

            Assert.Multiple(() =>
            {
                Assert.That(provider.Entry.DisplayName, Is.EqualTo(expected));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Initialize_TranslatesFabricateGeneratedAbilityName_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyAbilityProvider.Initialize), () =>
        {
            var provider = new DummyAbilityProvider("&CFabricate {{Y|lead slug}}");

            provider.Initialize();

            Assert.That(provider.Entry.DisplayName, Is.EqualTo("&C{{Y|lead slug}}を生成"));
        });
    }

    [Test]
    public void SyncAbility_TranslatesDynamicRunAbilityName_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyAbilityProvider.SyncAbility), () =>
        {
            var provider = new DummyAbilityProvider("Run");

            provider.SyncAbility(silent: true);

            Assert.That(provider.Entry.DisplayName, Is.EqualTo("走る"));
        });
    }

    [Test]
    public void Initialize_LeavesUnknownRegisteredAbilityNameUnchanged_WhenPatched()
    {
        WithPatchedOwner(nameof(DummyAbilityProvider.Initialize), () =>
        {
            var provider = new DummyAbilityProvider("Unknown Ability");

            provider.Initialize();

            Assert.Multiple(() =>
            {
                Assert.That(provider.Entry.DisplayName, Is.EqualTo("Unknown Ability"));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    private static void WithPatchedOwner(string methodName, Action action)
    {
        var harmonyId = "qudjp.tests.activated-ability-misc-provider." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireOwnerMethod(methodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ActivatedAbilityMiscProviderTranslationPatch),
                    nameof(ActivatedAbilityMiscProviderTranslationPatch.Postfix),
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
            ActivatedAbilityMiscProviderTranslationPatch.Context,
            ActivatedAbilityMiscProviderTranslationPatch.Family);
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return string.Equals(methodName, nameof(DummyAbilityProvider.SyncAbility), StringComparison.Ordinal)
            ? RequireMethod(typeof(DummyAbilityProvider), methodName, typeof(bool))
            : RequireMethod(typeof(DummyAbilityProvider), methodName);
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

internal sealed class DummyAbilityProvider
{
    public DummyAbilityProvider(string registeredName)
    {
        RegisteredName = registeredName;
        Entry = new DummyActivatedAbilityEntry { ID = Guid.NewGuid(), DisplayName = registeredName };
    }

    public Guid ActivatedAbilityID { get; private set; }

    public DummyActivatedAbilityEntry Entry { get; }

    private string RegisteredName { get; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Initialize()
    {
        ActivatedAbilityID = Entry.ID;
        Entry.DisplayName = RegisteredName;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SyncAbility(bool silent = false)
    {
        _ = silent;
        ActivatedAbilityID = Entry.ID;
        Entry.DisplayName = RegisteredName;
    }

    public DummyActivatedAbilityEntry? MyActivatedAbility(Guid id)
    {
        return id == Entry.ID ? Entry : null;
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

internal sealed class DummyActivatedAbilityEntry
{
    public Guid ID { get; init; }

    public string DisplayName { get; set; } = string.Empty;
}

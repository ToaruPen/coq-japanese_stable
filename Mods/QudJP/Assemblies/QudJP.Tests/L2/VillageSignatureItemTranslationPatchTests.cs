using System.Reflection;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class VillageSignatureItemTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-village-signature-item-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase("the oldest folded paper crane", "最古の折り紙の鶴")]
    [TestCase("the purest carbide hammer", "最も純粋な超硬合金のハンマー")]
    [TestCase("the ceremonial dagger of Resheph", "Reshephの儀礼用短剣")]
    public void Postfix_TranslatesSignatureHistoricObjectDisplayName(string source, string expected)
    {
        WriteDictionary(
            ("folded paper crane", "折り紙の鶴"),
            ("carbide hammer", "超硬合金のハンマー"),
            ("ceremonial dagger", "儀礼用短剣"));

        RunWithPatch(() =>
        {
            var target = new DummyVillageSignatureItemsTarget(source);
            target.generateSignatureItems();

            Assert.Multiple(() =>
            {
                Assert.That(target.signatureHistoricObjectInstance!.DisplayName, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(VillageSignatureItemTranslationPatch),
                        "VillageSignatureItem.HistoricObjectDisplayName"),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Postfix_LeavesUnknownDisplayNameUntouched()
    {
        RunWithPatch(() =>
        {
            var target = new DummyVillageSignatureItemsTarget("the mysterious bauble");
            target.generateSignatureItems();

            Assert.Multiple(() =>
            {
                Assert.That(target.signatureHistoricObjectInstance!.DisplayName, Is.EqualTo("the mysterious bauble"));
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Postfix_LeavesEmptyDisplayNameUntouched()
    {
        RunWithPatch(() =>
        {
            var target = new DummyVillageSignatureItemsTarget(string.Empty);
            target.generateSignatureItems();

            Assert.Multiple(() =>
            {
                Assert.That(target.signatureHistoricObjectInstance!.DisplayName, Is.Empty);
                Assert.That(RouteHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Postfix_PreservesColorTagsInTranslatedSignatureDisplayName()
    {
        WriteDictionary(("folded paper crane", "折り紙の鶴"));

        RunWithPatch(() =>
        {
            var target = new DummyVillageSignatureItemsTarget("the oldest {{C|folded paper crane}}");
            target.generateSignatureItems();

            Assert.Multiple(() =>
            {
                Assert.That(target.signatureHistoricObjectInstance!.DisplayName, Is.EqualTo("最古の{{C|折り紙の鶴}}"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkedSignatureDisplayName()
    {
        RunWithPatch(() =>
        {
            var target = new DummyVillageSignatureItemsTarget(
                MessageFrameTranslator.MarkDirectTranslation("the mysterious bauble"));
            target.generateSignatureItems();

            Assert.That(target.signatureHistoricObjectInstance!.DisplayName, Is.EqualTo("the mysterious bauble"));
        });
    }

    private static void RunWithPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyVillageSignatureItemsTarget), nameof(DummyVillageSignatureItemsTarget.generateSignatureItems)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(VillageSignatureItemTranslationPatch),
                    nameof(VillageSignatureItemTranslationPatch.Postfix),
                    typeof(object))));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(entries[index].key);
            builder.Append("\",\"text\":\"");
            builder.Append(entries[index].text);
            builder.Append("\"}");
        }

        builder.Append("]}\n");
        File.WriteAllText(Path.Combine(tempDirectory, "village-signature-item-l2.ja.json"), builder.ToString());
    }

    private static int RouteHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(VillageSignatureItemTranslationPatch),
            "VillageSignatureItem.HistoricObjectDisplayName");
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, parameters)
        ?? throw new MissingMethodException(type.FullName, name);

    private sealed class DummyVillageSignatureItemsTarget
    {
        private readonly string sourceDisplayName;

        public DummyVillageSignatureItemsTarget(string sourceDisplayName)
        {
            this.sourceDisplayName = sourceDisplayName;
        }

        public DummyGameObject? signatureHistoricObjectInstance;

        public void generateSignatureItems()
        {
            signatureHistoricObjectInstance = new DummyGameObject
            {
                DisplayName = sourceDisplayName,
            };
        }
    }

    private sealed class DummyGameObject
    {
        public string DisplayName = string.Empty;
    }
}

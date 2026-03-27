using System.Collections;
using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CharGenProducerTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-chargen-producer-l2", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void BreadcrumbPostfix_TranslatesReturnedTitle()
    {
        WriteDictionary(("Choose Game Mode", "：プレイ方式を選択："));

        RunWithBreadcrumbPostfix(() =>
        {
            var result = new DummyCharGenModuleWindowTarget { BreadcrumbTitle = "Choose Game Mode" }.GetBreadcrumb();
            Assert.That(result.Title, Is.EqualTo("：プレイ方式を選択："));
        });
    }

    [Test]
    public void BreadcrumbPostfix_PreservesFallbackAndEdgeCases()
    {
        WriteDictionary(("Choose Game Mode", "：プレイ方式を選択："));

        RunWithBreadcrumbPostfix(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    new DummyCharGenModuleWindowTarget { BreadcrumbTitle = "Untranslated Title" }.GetBreadcrumb().Title,
                    Is.EqualTo("Untranslated Title"),
                    "Missing entries should fall back to English.");
                Assert.That(
                    new DummyCharGenModuleWindowTarget { BreadcrumbTitle = string.Empty }.GetBreadcrumb().Title,
                    Is.EqualTo(string.Empty),
                    "Empty strings should pass through unchanged.");
                Assert.That(
                    new DummyCharGenModuleWindowTarget { BreadcrumbTitle = "\u0001Choose Game Mode" }.GetBreadcrumb().Title,
                    Is.EqualTo("\u0001Choose Game Mode"),
                    "Marker-prefixed strings should pass through unchanged.");
                Assert.That(
                    new DummyCharGenModuleWindowTarget { BreadcrumbTitle = "{{y|Choose Game Mode}}" }.GetBreadcrumb().Title,
                    Is.EqualTo("{{y|：プレイ方式を選択：}}"),
                    "Color-tagged strings should preserve tags while translating visible text.");
            });
        });
    }

    [Test]
    public void BreadcrumbPostfix_DoesNotDoubleTranslateAlreadyTranslatedOutput()
    {
        WriteDictionary(
            ("Choose Game Mode", "Play Mode"),
            ("Play Mode", "プレイ方式"));

        RunWithBreadcrumbPostfix(() =>
        {
            var result = new DummyCharGenModuleWindowTarget { BreadcrumbTitle = "Choose Game Mode" }.GetBreadcrumb();
            Assert.That(result.Title, Is.EqualTo("Play Mode"));
        });
    }

    [Test]
    public void MenuOptionPostfix_TranslatesReturnedDescriptions()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));

        RunWithMenuOptionPostfix(() =>
        {
            var target = new DummyCharGenModuleWindowTarget();
            target.MenuOptions.Add(new DummyCharGenMenuOption { Description = "{{y|Points Remaining: 12}}" });

            var result = target.GetKeyMenuBar().ToList();
            Assert.That(result[0].Description, Is.EqualTo("{{y|残りポイント: 12}}"));
        });
    }

    [Test]
    public void MenuOptionPostfix_PreservesFallbackAndEdgeCases()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));

        RunWithMenuOptionPostfix(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    TranslateMenuOptionDescription("Untranslated Description"),
                    Is.EqualTo("Untranslated Description"),
                    "Missing entries should fall back to English.");
                Assert.That(
                    TranslateMenuOptionDescription(string.Empty),
                    Is.EqualTo(string.Empty),
                    "Empty strings should pass through unchanged.");
                Assert.That(
                    TranslateMenuOptionDescription("\u0001Points Remaining: 12"),
                    Is.EqualTo("\u0001Points Remaining: 12"),
                    "Marker-prefixed strings should pass through unchanged.");
                Assert.That(
                    TranslateMenuOptionDescription("{{y|Points Remaining: 12}}"),
                    Is.EqualTo("{{y|残りポイント: 12}}"),
                    "Color-tagged strings should preserve tags while translating visible text.");
            });
        });
    }

    [Test]
    public void MenuOptionPostfix_ReturnsNullWhenSourceEnumerableIsNull()
    {
        Assert.That(CharGenMenuOptionTranslationPatch.Postfix(null!), Is.Null);
    }

    [Test]
    public void ChromePrefix_TranslatesDescriptorAndCategoryTitles()
    {
        WriteDictionary(
            ("Choose Game Mode", "：プレイ方式を選択："),
            ("Choose calling", "職能を選択"));

        RunWithChromePrefix(() =>
        {
            AssertChromeTitles("Choose Game Mode", "Choose calling", "：プレイ方式を選択：", "職能を選択");
        });
    }

    [Test]
    public void ChromePrefix_PreservesFallbackAndEdgeCases()
    {
        WriteDictionary(
            ("Choose Game Mode", "：プレイ方式を選択："),
            ("Choose calling", "職能を選択"));

        RunWithChromePrefix(() =>
        {
            AssertChromeTitles("Untranslated Title", "Untranslated Category", "Untranslated Title", "Untranslated Category");
            AssertChromeTitles(string.Empty, string.Empty, string.Empty, string.Empty);
            AssertChromeTitles("\u0001Choose Game Mode", "\u0001Choose calling", "\u0001Choose Game Mode", "\u0001Choose calling");
            AssertChromeTitles("{{y|Choose Game Mode}}", "{{y|Choose calling}}", "{{y|：プレイ方式を選択：}}", "{{y|職能を選択}}");
        });
    }

    private static void RunWithBreadcrumbPostfix(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenModuleWindowTarget), nameof(DummyCharGenModuleWindowTarget.GetBreadcrumb)),
                postfix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.CharGenBreadcrumbTranslationPatch",
                    "Postfix",
                    typeof(object))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithMenuOptionPostfix(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenModuleWindowTarget), nameof(DummyCharGenModuleWindowTarget.GetKeyMenuBar)),
                postfix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.CharGenMenuOptionTranslationPatch",
                    "Postfix",
                    typeof(IEnumerable))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithChromePrefix(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyCharGenFrameworkScrollerTarget),
                    nameof(DummyCharGenFrameworkScrollerTarget.BeforeShow)),
                prefix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.CharGenChromeTranslationPatch",
                    "Prefix",
                    typeof(object[]),
                    typeof(MethodBase))));
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyCharGenCategoryMenuControllerTarget),
                    nameof(DummyCharGenCategoryMenuControllerTarget.setData)),
                prefix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.CharGenChromeTranslationPatch",
                    "Prefix",
                    typeof(object[]),
                    typeof(MethodBase))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertChromeTitles(
        string descriptorTitle,
        string categoryTitle,
        string expectedDescriptorTitle,
        string expectedCategoryTitle)
    {
        var descriptor = new DummyEmbarkBuilderModuleWindowDescriptor { title = descriptorTitle };
        var scroller = new DummyCharGenFrameworkScrollerTarget();
        scroller.BeforeShow(descriptor, selections: null);

        var category = new DummyFrameworkDataElement { Title = categoryTitle };
        var controller = new DummyCharGenCategoryMenuControllerTarget();
        controller.setData(category);

        Assert.Multiple(() =>
        {
            Assert.That(scroller.LastTitle, Is.EqualTo(expectedDescriptorTitle));
            Assert.That(controller.LastTitle, Is.EqualTo(expectedCategoryTitle));
        });
    }

    private static string? TranslateMenuOptionDescription(string source)
    {
        var target = new DummyCharGenModuleWindowTarget();
        target.MenuOptions.Add(new DummyCharGenMenuOption { Description = source });

        var result = target.GetKeyMenuBar().ToList();
        return result[0].Description;
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        var method = AccessTools.Method(type, methodName);
        Assert.That(method, Is.Not.Null, $"Method not found: {type.FullName}.{methodName}");
        return method!;
    }

    private static MethodInfo RequirePatchMethod(string typeName, string methodName, params Type[] parameterTypes)
    {
        var patchType = typeof(Translator).Assembly.GetType(typeName, throwOnError: false);
        Assert.That(patchType, Is.Not.Null, $"Patch type not found: {typeName}");

        var method = patchType!.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"Method not found: {typeName}.{methodName}");
        return method!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(dictionariesDirectory, "chargen-producer-l2.ja.json");
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}

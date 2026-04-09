using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public void SubtypeSelectionPostfix_TranslatesCallingCarouselDescriptions()
    {
        WriteDictionary(
            ("Short Blade", "短剣"),
            ("Tinkering", "修理"),
            ("Scavenger", "廃品漁り"),
            ("Acrobatics", "軽業"),
            ("Spry", "身軽"),
            ("Starts with random junk and artifacts", "ランダムなガラクタとアーティファクトを所持して開始"));

        RunWithSubtypeSelectionPostfix(() =>
        {
            var result = new DummyCharGenSubtypeModuleTarget().GetSelections().ToList();
            var expected = """
                {{c|ù}} 敏捷 +2
                {{c|ù}} 短剣
                {{c|ù}} 修理
                  {{C|ù}} 廃品漁り
                {{c|ù}} 軽業
                  {{C|ù}} 身軽
                {{c|ù}} ランダムなガラクタとアーティファクトを所持して開始
                """;

            Assert.That(result[0].Description, Is.EqualTo(expected));
        });
    }

    [Test]
    public void SubtypeSelectionPostfix_PreservesFallbackAndEdgeCases()
    {
        WriteDictionary(("Short Blade", "短剣"));

        RunWithSubtypeSelectionPostfix(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    TranslateSubtypeSelectionDescription("{{c|ù}} Untranslated Description"),
                    Is.EqualTo("{{c|ù}} Untranslated Description"),
                    "Missing entries should fall back to English.");
                Assert.That(
                    TranslateSubtypeSelectionDescription(string.Empty),
                    Is.EqualTo(string.Empty),
                    "Empty strings should pass through unchanged.");
                Assert.That(
                    TranslateSubtypeSelectionDescription("{{c|ù}} Short Blade"),
                    Is.EqualTo("{{c|ù}} 短剣"),
                    "Color-tagged bullet lines should preserve tags while translating visible text.");
                Assert.That(
                    TranslateSubtypeSelectionDescription("\u0001{{c|ù}} Short Blade"),
                    Is.EqualTo("\u0001{{c|ù}} Short Blade"),
                    "Marker-prefixed strings should pass through unchanged.");
            });
        });
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

    [Test]
    public void CustomizeTranspiler_TranslatesSelectionPrefixesAndPopupStrings()
    {
        WriteDictionary(
            ("Gender: ", "性別："),
            ("Pronoun Set: ", "代名詞セット："),
            ("Pet: ", "ペット："),
            ("Enter name:", "名前を入力："),
            ("Choose Gender", "性別を選択"),
            ("Choose Pronoun Set", "代名詞セットを選択"),
            ("Choose Pet", "ペットを選択"),
            ("<create new>", "<新規作成>"),
            ("<from gender>", "<性別に従う>"),
            ("<none>", "<なし>"));

        RunWithCustomizeTranspiler(() =>
        {
            var target = new DummyCharGenCustomizeWindowTarget();
            var selections = target.GetSelections().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(selections[0].Prefix, Is.EqualTo("性別："));
                Assert.That(selections[1].Prefix, Is.EqualTo("代名詞セット："));
                Assert.That(selections[2].Prefix, Is.EqualTo("ペット："));
            });

            DummyCharGenCustomizeWindowTarget.ShowNamePromptAsync().GetAwaiter().GetResult();
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo("名前を入力："));

            DummyCharGenCustomizeWindowTarget.ShowChooseGenderAsync().GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("性別を選択"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.EqualTo(new[] { "<新規作成>" }));
            });

            DummyCharGenCustomizeWindowTarget.ShowChoosePronounSetAsync().GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("代名詞セットを選択"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.EqualTo(new[] { "<性別に従う>", "<新規作成>" }));
            });

            DummyCharGenCustomizeWindowTarget.ShowChoosePetAsync().GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("ペットを選択"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.EqualTo(new[] { "<なし>" }));
            });
        });
    }

    [Test]
    public void CustomizeTranspiler_PreservesFallbackAndEdgeCases()
    {
        WriteDictionary(
            ("Gender: ", "性別："),
            ("Choose Gender", "性別を選択"),
            ("<create new>", "<新規作成>"));

        RunWithCustomizeTranspiler(() =>
        {
            var target = new DummyCharGenCustomizeWindowTarget();
            var selections = target.GetSelections().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(selections[0].Prefix, Is.EqualTo("性別："));
                Assert.That(selections[1].Prefix, Is.EqualTo("Pronoun Set: "));
                Assert.That(selections[2].Prefix, Is.EqualTo("Pet: "));
            });

            DummyCharGenCustomizeWindowTarget.ShowEmptyPromptAsync().GetAwaiter().GetResult();
            Assert.That(DummyPopupTarget.LastShowBlockMessage, Is.EqualTo(string.Empty));

            DummyCharGenCustomizeWindowTarget.ShowMarkedChooseGenderAsync().GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("\u0001Choose Gender"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.EqualTo(new[] { "\u0001<create new>" }));
            });

            DummyCharGenCustomizeWindowTarget.ShowColorTaggedChooseGenderAsync().GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupTarget.LastOptionListTitle, Is.EqualTo("{{y|性別を選択}}"));
                Assert.That(DummyPopupTarget.LastOptionListOptions, Is.EqualTo(new[] { "{{y|<新規作成>}}" }));
            });
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

    private static void RunWithSubtypeSelectionPostfix(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenSubtypeModuleTarget), nameof(DummyCharGenSubtypeModuleTarget.GetSelections)),
                postfix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.CharGenSubtypeSelectionTranslationPatch",
                    "Postfix",
                    typeof(IEnumerable))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithCustomizeTranspiler(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.GetSelections));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowNamePromptAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowChooseGenderAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowChoosePronounSetAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowChoosePetAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowEmptyPromptAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowMarkedChooseGenderAsync));
            PatchStateMachineMoveNext(
                harmony,
                typeof(DummyCharGenCustomizeWindowTarget),
                nameof(DummyCharGenCustomizeWindowTarget.ShowColorTaggedChooseGenderAsync));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchStateMachineMoveNext(Harmony harmony, Type targetType, string methodName)
    {
        var sourceMethod = RequireMethod(targetType, methodName);
        var targetMethod = ResolveStateMachineMoveNext(sourceMethod);
        Assert.That(targetMethod, Is.Not.Null, $"State machine MoveNext not found for {targetType.FullName}.{methodName}");

        harmony.Patch(
            original: targetMethod!,
            transpiler: new HarmonyMethod(RequirePatchMethod(
                "QudJP.Patches.CharGenCustomizeTranslationPatch",
                "Transpiler",
                typeof(IEnumerable<CodeInstruction>))));
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

    private static string? TranslateSubtypeSelectionDescription(string source)
    {
        var translatedSelections = new DummyCharGenSubtypeModuleTarget { Description = source }
            .GetSelections()
            .ToList();
        return translatedSelections.Single().Description;
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

    private static MethodInfo? ResolveStateMachineMoveNext(MethodInfo sourceMethod)
    {
        var asyncStateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (asyncStateMachine?.StateMachineType is not null)
        {
            return AccessTools.Method(asyncStateMachine.StateMachineType, "MoveNext");
        }

        var iteratorStateMachine = sourceMethod.GetCustomAttribute<IteratorStateMachineAttribute>();
        if (iteratorStateMachine?.StateMachineType is not null)
        {
            return AccessTools.Method(iteratorStateMachine.StateMachineType, "MoveNext");
        }

        return null;
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

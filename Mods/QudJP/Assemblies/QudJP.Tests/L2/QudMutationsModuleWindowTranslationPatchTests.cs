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
public sealed class QudMutationsModuleWindowTranslationPatchTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-mutationwindow-l2", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupMessageTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupMessageTarget.Reset();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void Patch_TranslatesMutationMenuRowsBeforeShowAndUpdatesLongDescription()
    {
        WriteXmlFile(
            "Mutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations>
              <category Name="Physical" DisplayName="{{G|肉体突然変異}}">
                <mutation Name="Adrenal Control" DisplayName="アドレナリン制御" />
                <mutation Name="Stinger (Confusing Venom)" DisplayName="毒針（混乱毒）" />
              </category>
              <category Name="Mental" DisplayName="{{M|精神突然変異}}">
                <mutation Name="Esper" DisplayName="エスパー" />
              </category>
            </mutations>
            """);
        WriteDictionary(
            ("mutation:Esper", "精神突然変異しか発現しない。"),
            ("mutation:Adrenal Control", "アドレナリン分泌を制御できる。"),
            ("mutation:Adrenal Control:rank:1", "クールダウン: 200ターン"),
            ("mutation:Stinger (Confusing Venom)", "臀部の毒針を持つ。"),
            ("mutation:Stinger (Confusing Venom):Stinger Confusion:rank:1", "混乱毒を与える針攻撃。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            var transpiler = AccessTools.Method(
                typeof(QudMutationsModuleWindowTranslationPatch),
                "Transpiler",
                [typeof(IEnumerable<CodeInstruction>)]);
            Assert.That(transpiler, Is.Not.Null, "Transpiler method should be found");

            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMutationsModuleWindow), nameof(DummyQudMutationsModuleWindow.UpdateControls)),
                transpiler: new HarmonyMethod(transpiler!),
                postfix: new HarmonyMethod(RequireMethod(typeof(QudMutationsModuleWindowTranslationPatch), nameof(QudMutationsModuleWindowTranslationPatch.Postfix))));

            var window = new DummyQudMutationsModuleWindow();
            window.UpdateControls();

            var esper = window.categoryMenus[0].menuOptions[0];
            var adrenalControl = window.categoryMenus[0].menuOptions[1];
            var stinger = window.categoryMenus[0].menuOptions[2];

            Assert.Multiple(() =>
            {
                Assert.That(window.prefabComponent.LastRenderedDescriptions, Is.EqualTo(new[]
                {
                    "エスパー",
                    "アドレナリン制御",
                    "毒針（混乱毒） [{{W|V}}]",
                }));
                Assert.That(esper.Description, Is.EqualTo("エスパー"));
                Assert.That(esper.LongDescription, Is.EqualTo("精神突然変異しか発現しない。"));
                Assert.That(adrenalControl.Description, Is.EqualTo("アドレナリン制御"));
                Assert.That(adrenalControl.LongDescription, Is.EqualTo("アドレナリン分泌を制御できる。\n\nクールダウン: 200ターン"));
                Assert.That(stinger.Description, Is.EqualTo("毒針（混乱毒） [{{W|V}}]"));
                Assert.That(stinger.LongDescription, Is.EqualTo("臀部の毒針を持つ。\n\n混乱毒を与える針攻撃。"));
                Assert.That(window.prefabComponent.LastRenderedLongDescriptions[2], Is.EqualTo("臀部の毒針を持つ。\n\n混乱毒を与える針攻撃。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslateFormattedDescription_ReturnsSource_ForMissingDictionaries()
    {
        Translator.SetDictionaryDirectoryForTests(Path.Combine(tempRoot, "missing-dictionaries"));

        var source = "Esper [{{W|V}}]";
        var translated = QudMutationsModuleWindowTranslationPatch.TranslateFormattedDescription(source);

        Assert.That(translated, Is.EqualTo(source));
    }

    [TestCase("Freezing Ray", "Icy Vapor", "任意の方向へ冷気の光線を放つ。", "選んだ方向に9マスの冷気線を放つ。")]
    [TestCase("Flaming Ray", "Ghostly Flames", "任意の方向へ火炎の光線を放つ。", "選んだ方向に9マスの火炎線を放つ。")]
    [TestCase("Horns", "Horns Antlers", "頭から鋭い角が生えている。", "枝角で敵を突く。")]
    public void Postfix_UsesMutationNodeVariantForLongDescriptionRankText(
        string mutationName,
        string variant,
        string description,
        string rankText)
    {
        WriteDictionary(
            ($"mutation:{mutationName}", description),
            ($"mutation:{mutationName}:{variant}:rank:1", rankText));

        var window = new DummyQudMutationsModuleWindow
        {
            categoryMenus =
            [
                new DummyMutationCategoryMenuData(
                    new DummyMutationMenuOption(
                        mutationName,
                        mutationName,
                        "English description that should be replaced.")),
            ],
            mutationNodes =
            [
                new DummyMutationNode(mutationName, variant),
            ],
        };

        QudMutationsModuleWindowTranslationPatch.Postfix(window);

        Assert.Multiple(() =>
        {
            Assert.That(window.categoryMenus[0].menuOptions[0].LongDescription, Is.EqualTo($"{description}\n\n{rankText}"));
            Assert.That(window.prefabComponent.LastRenderedLongDescriptions[0], Is.EqualTo($"{description}\n\n{rankText}"));
        });
    }

    [Test]
    public void PopupPrefix_TranslatesVariantPickerTitle_WhenSelectVariantOwnerPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPickOption(harmony);
            var ownerOriginal = ResolveStateMachineMoveNext(RequireMethod(
                typeof(DummyQudMutationsModuleWindow),
                nameof(DummyQudMutationsModuleWindow.SelectVariant)))
                ?? throw new InvalidOperationException("Dummy SelectVariant state machine not found.");
            harmony.Patch(
                original: ownerOriginal,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(QudMutationsModuleWindowVariantPopupTranslationPatch),
                    nameof(QudMutationsModuleWindowVariantPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(QudMutationsModuleWindowVariantPopupTranslationPatch),
                    nameof(QudMutationsModuleWindowVariantPopupTranslationPatch.Finalizer),
                    typeof(Exception))));

            DummyQudMutationsModuleWindow.StaticPopupTitleToShow = "Choose variant";
            DummyQudMutationsModuleWindow.SelectVariant().GetAwaiter().GetResult();

            Assert.That(DummyPopupGenericTarget.LastPickOptionTitle, Is.EqualTo("変種を選択"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            DummyPopupGenericTarget.Reset();
            DummyQudMutationsModuleWindow.StaticPopupTitleToShow = "Choose variant";
        }
    }

    [Test]
    public void PopupPrefix_TranslatesShowPointsTitle_WhenHandleMenuOptionOwnerPatched()
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupMessage(harmony);
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyQudMutationsModuleWindow),
                    nameof(DummyQudMutationsModuleWindow.HandleMenuOption)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch),
                    nameof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(
                    typeof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch),
                    nameof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch.Finalizer),
                    typeof(Exception))));

            DummyQudMutationsModuleWindow.StaticSummaryPopupMessageToShow = "Mutation summary";
            DummyQudMutationsModuleWindow.StaticPointsRemainingToShow = 3;
            new DummyQudMutationsModuleWindow().HandleMenuOption("ShowPoints");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupMessageTarget.LastMessage, Is.EqualTo("Mutation summary"));
                Assert.That(DummyPopupMessageTarget.LastTitle, Is.EqualTo("変異ポイント残り: 3"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupMessageTranslationPatch),
                        "Popup.ProducerText."
                        + nameof(QudMutationsModuleWindowHandleMenuOptionPopupTranslationPatch)
                        + ".PointsRemainingTitle"),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            DummyPopupMessageTarget.Reset();
            DummyQudMutationsModuleWindow.StaticSummaryPopupMessageToShow = "Mutation summary";
            DummyQudMutationsModuleWindow.StaticPointsRemainingToShow = 2;
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return AccessTools.Method(type, methodName, parameterTypes)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void PatchPickOption(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupPickOptionTranslationPatch),
                nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(PopupPickOptionTranslationPatch),
                nameof(PopupPickOptionTranslationPatch.Finalizer))));
    }

    private static void PatchPopupMessage(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupMessageTranslationPatch),
                nameof(PopupMessageTranslationPatch.Prefix))));
    }

    private static MethodInfo? ResolveStateMachineMoveNext(MethodInfo sourceMethod)
    {
        var stateMachine = sourceMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        return stateMachine?.StateMachineType is null
            ? null
            : AccessTools.Method(stateMachine.StateMachineType, "MoveNext");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(
            Path.Combine(dictionariesDirectory, "mutation-window-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteXmlFile(string relativePath, string content)
    {
        File.WriteAllText(
            Path.Combine(tempRoot, relativePath),
            content.ReplaceLineEndings(Environment.NewLine),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

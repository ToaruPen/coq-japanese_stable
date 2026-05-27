using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireCookFromIngredientsTranslationPatchTests
{
    private string tempDictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDictionaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-campfire-cook-from-ingredients-l2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDictionaryDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDictionaryDirectory);
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        DummyPopupGenericTarget.Reset();

        if (Directory.Exists(tempDictionaryDirectory))
        {
            Directory.Delete(tempDictionaryDirectory, recursive: true);
        }
    }

    [TestCase(
        "You create a new recipe for {{|Glowfish Stew}}!",
        "{{|Glowfish Stew}}の新しいレシピを作った！",
        "RecipeCreated")]
    [TestCase(
        "You eat the meal.",
        "食事をとった。",
        "AteMeal")]
    [TestCase(
        "You toss {{Y|snapjaw haunch}} into a pot and stir.",
        "{{Y|snapjaw haunch}}を鍋に放り込み、かき混ぜた。",
        "MealDescription")]
    [TestCase(
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|+1 to hit}}",
        "食事の代謝が始まり、一日中次の効果を得る:\n\n{{W|命中+1}}",
        "MetabolizeMeal")]
    public void CookFromIngredients_TranslatesOwnerPopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = source,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookFromIngredients_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.Show("You create a new recipe for {{|Glowfish Stew}}!");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.Not.Null);
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You create a new recipe for {{|Glowfish Stew}}!";

        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = string.Empty,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
                Assert.That(HitCount("MetabolizeMeal"), Is.Zero);
            });
        });
    }

    [TestCase("You aren't hungry. Instead, you relax by the warmth of the fire.")]
    [TestCase("You don't have the Cooking and Gathering skill.")]
    [TestCase("Are you sure you want to forget this recipe?")]
    [TestCase("A savory meal made from {{Y|snapjaw haunch}}.")]
    [TestCase("You eat the meal. It's tastier than usual.")]
    public void CookFromIngredients_DoesNotClaimDeferredFixedOrRuntimePopups_WhenOwnerPatched(string source)
    {
        WithPatchedOwner(() =>
        {
            new DummyCampfireCookFromIngredientsTarget
            {
                PopupMessageToShow = source,
            }.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.Not.Null);
                Assert.That(HitCount("AteMeal"), Is.Zero);
                Assert.That(HitCount("MealDescription"), Is.Zero);
                Assert.That(HitCount("RecipeCreated"), Is.Zero);
                Assert.That(HitCount("MetabolizeMeal"), Is.Zero);
            });
        });
    }

    [TestCase(
        "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}",
        "{{W|選択した材料{{C|0}}個で料理する。}}\n{{y|[あと2個まで]}}")]
    [TestCase(
        "{{W|Cook with the {{R|3}} selected ingredients.}}\n{{y|[0 remaining]}}",
        "{{W|選択した材料{{R|3}}個で料理する。}}\n{{y|[残り0個]}}")]
    public void CookFromIngredients_TranslatesSelectedIngredientMenuRows_WhenOwnerActive(
        string source,
        string expected)
    {
        WithPatchedOwner(() =>
        {
            var target = new DummyCampfireCookFromIngredientsTarget
            {
                PickOptionOptionsToShow = new[] { source },
            };

            target.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions![0], Is.EqualTo(expected));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookFromIngredients_TranslatesSelectedIngredientMenuRows_WithCrLfLineEndings()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\r\n{{y|[up to 2 remaining]}}";

        WithPatchedOwner(() =>
        {
            var target = new DummyCampfireCookFromIngredientsTarget
            {
                PickOptionOptionsToShow = new[] { source },
            };

            target.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);
                Assert.That(
                    DummyPopupGenericTarget.LastPickOptionOptions![0],
                    Is.EqualTo("{{W|選択した材料{{C|0}}個で料理する。}}\n{{y|[あと2個まで]}}"));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        "[ ]   a dram of brackish water {{K|x29}}",
        "[ ]   塩気混じりの水1ドラム {{K|x29}}")]
    [TestCase(
        "{{y|[{{G|X}}]}}   {{Y|starapple jam}} {{K|x3}}",
        "{{y|[{{G|X}}]}}   {{Y|スターアップルジャム}} {{K|x3}}")]
    public void CookFromIngredients_TranslatesIngredientOptionRows_WhenOwnerActive(string source, string expected)
    {
        WriteHistorySpiceCommonDictionary(
            ("brackish water", "塩気混じりの水"),
            ("starapple jam", "スターアップルジャム"));

        WithPatchedOwner(() =>
        {
            var target = new DummyCampfireCookFromIngredientsTarget
            {
                PickOptionOptionsToShow = new[] { source },
            };

            target.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions![0], Is.EqualTo(expected));
                Assert.That(PickOptionProducerHitCount("IngredientOptionMenuRow"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void CookFromIngredients_DoesNotTranslateSelectedIngredientMenuRows_WhenOwnerAbsent()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}";

        WithPatchedPickOptionOnly(() =>
        {
            _ = DummyPopupGenericTarget.PickOption(
                Title: "Choose ingredients to cook with.",
                Options: new[] { source },
                MaxWidth: 60,
                DefaultSelected: 0,
                IconPosition: 6,
                AllowEscape: true);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions![0], Is.EqualTo(source));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_StripsDirectMarkedSelectedIngredientMenuRow_WhenOwnerActive()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}";

        WithPatchedOwner(() =>
        {
            var target = new DummyCampfireCookFromIngredientsTarget
            {
                PickOptionOptionsToShow = new[] { MessageFrameTranslator.MarkDirectTranslation(source) },
            };

            target.CookFromIngredients(random: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions, Is.Not.Null);
                Assert.That(DummyPopupGenericTarget.LastPickOptionOptions![0], Is.EqualTo(source));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.Zero);
            });
        });
    }

    [Test]
    public void CookFromIngredients_StripsDirectMarkedSelectedIngredientMenuRow_InOwnerProducerHandler()
    {
        const string source = "{{W|Cook with the {{C|0}} selected ingredients.}}\n{{y|[up to 2 remaining]}}";

        CampfireCookFromIngredientsTranslationPatch.Prefix(out var state);
        try
        {
            var handled = CampfireCookFromIngredientsTranslationPatch.TryTranslatePopupProducerText(
                MessageFrameTranslator.MarkDirectTranslation(source),
                nameof(PopupPickOptionTranslationPatch),
                "Popup.ProducerText",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(translated, Is.EqualTo(source));
                Assert.That(PickOptionProducerHitCount("SelectedIngredientsMenuRow"), Is.Zero);
            });
        }
        finally
        {
            CampfireCookFromIngredientsTranslationPatch.Finalizer(null, state);
        }
    }

    [Test]
    public void CookFromIngredients_RestoresDirectMarkerPassThroughText_ForNestedOwnerScopes()
    {
        CampfireCookFromIngredientsTranslationPatch.Prefix(out var outerState);
        try
        {
            _ = CampfireCookFromIngredientsTranslationPatch.TryTranslatePopupMessage(
                MessageFrameTranslator.MarkDirectTranslation("You eat the meal."),
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out _);

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));

            CampfireCookFromIngredientsTranslationPatch.Prefix(out var innerState);
            try
            {
                _ = CampfireCookFromIngredientsTranslationPatch.TryTranslatePopupMessage(
                    MessageFrameTranslator.MarkDirectTranslation("Nested direct popup."),
                    nameof(PopupShowTranslationPatch),
                    "Popup.Show",
                    out _);

                Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("Nested direct popup."));
            }
            finally
            {
                CampfireCookFromIngredientsTranslationPatch.Finalizer(null, innerState);
            }

            Assert.That(DirectMarkerPassThroughText(), Is.EqualTo("You eat the meal."));
        }
        finally
        {
            CampfireCookFromIngredientsTranslationPatch.Finalizer(null, outerState);
        }

        Assert.That(DirectMarkerPassThroughText(), Is.Null);
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupPickOption(harmony);
            PatchOwner(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPickOptionOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupPickOption(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteHistorySpiceCommonDictionary(params (string key, string text)[] entries)
    {
        var scopedDirectory = Path.Combine(tempDictionaryDirectory, "Scoped");
        Directory.CreateDirectory(scopedDirectory);
        var lines = entries.Select(entry => $"    {{ \"key\": \"{entry.key}\", \"text\": \"{entry.text}\" }}");
        File.WriteAllText(
            Path.Combine(scopedDirectory, "historyspice-common.ja.json"),
            "{\n  \"entries\": [\n" + string.Join(",\n", lines) + "\n  ]\n}\n");
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PopupShowTranslationPatch),
                nameof(PopupShowTranslationPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(MethodBase))));
    }

    private static void PatchPopupPickOption(Harmony harmony)
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

    private static void PatchOwner(Harmony harmony)
    {
        var prefix = new HarmonyMethod(RequireMethod(
            typeof(CampfireCookFromIngredientsTranslationPatch),
            nameof(CampfireCookFromIngredientsTranslationPatch.Prefix),
            typeof(string).MakeByRefType()));
        var finalizer = new HarmonyMethod(RequireMethod(
            typeof(CampfireCookFromIngredientsTranslationPatch),
            nameof(CampfireCookFromIngredientsTranslationPatch.Finalizer),
            typeof(Exception),
            typeof(string)));

        harmony.Patch(
            original: RequireMethod(
                typeof(DummyCampfireCookFromIngredientsTarget),
                nameof(DummyCampfireCookFromIngredientsTarget.CookFromIngredients),
                typeof(bool)),
            prefix: prefix,
            finalizer: finalizer);
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(CampfireCookFromIngredientsTranslationPatch) + "." + detail);
    }

    private static int PickOptionProducerHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupPickOptionTranslationPatch),
            "Popup.ProducerText." + nameof(CampfireCookFromIngredientsTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            var methodByName = AccessTools.Method(type, name);
            Assert.That(methodByName, Is.Not.Null, $"{type.FullName}.{name} not found");
            return methodByName!;
        }

        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static string CreateHarmonyId()
    {
        return "qudjp.tests.campfire-cook-from-ingredients." + Guid.NewGuid().ToString("N");
    }

    private static string? DirectMarkerPassThroughText()
    {
        var field = typeof(CampfireCookFromIngredientsTranslationPatch).GetField(
            "directMarkerPassThroughText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return field!.GetValue(null) as string;
    }
}

internal sealed class DummyCampfireCookFromIngredientsTarget
{
    public string PopupMessageToShow { get; set; } = string.Empty;

    public IReadOnlyList<string>? PickOptionOptionsToShow { get; set; }

    public Action? BeforePopup { get; set; }

    public bool CookFromIngredients(bool random)
    {
        _ = random;
        BeforePopup?.Invoke();
        if (PickOptionOptionsToShow is not null)
        {
            _ = DummyPopupGenericTarget.PickOption(
                Title: "Choose ingredients to cook with.",
                Options: PickOptionOptionsToShow,
                MaxWidth: 60,
                DefaultSelected: 0,
                IconPosition: 6,
                AllowEscape: true);
            return true;
        }

        DummyPopupShow.Show(PopupMessageToShow);
        return true;
    }
}

#if HAS_GAME_DLL
using QudJP.QudTest;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class QudTestPatchBindingHarnessTests
{
    private string fixturesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        fixturesDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixturesDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(fixturesDirectory))
        {
            Directory.Delete(fixturesDirectory, recursive: true);
        }
    }

    [Test]
    public void Run_PatchBindingResolvesTargetMethodSignature()
    {
        WriteFixture(
            "bindings-smoke.json",
            suite: "bindings",
            """
            {
              "id":"binding.campfire-describe-meal",
              "route":"patch-binding",
              "patch":"QudJP.Patches.CampfireDescribeMealTranslationPatch",
              "expectedTargets":[
                "XRL.World.Parts.Campfire|DescribeMeal|System.String|System.Collections.Generic.IReadOnlyList`1[[XRL.World.GameObject]]"
              ]
            }
            """);

        var result = QudTestRunner.Run("qudtest:bindings", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].Expected, Is.EqualTo("XRL.World.Parts.Campfire|DescribeMeal|System.String|System.Collections.Generic.IReadOnlyList`1[[XRL.World.GameObject]]"));
            Assert.That(result.Cases[0].Actual, Is.EqualTo(result.Cases[0].Expected));
        });
    }

    [Test]
    public void Run_PatchBindingResolvesTargetMethodsSignatures()
    {
        WriteFixture(
            "bindings-smoke.json",
            suite: "bindings",
            """
            {
              "id":"binding.campfire-preserve",
              "route":"patch-binding",
              "patch":"QudJP.Patches.CampfirePreserveTranslationPatch",
              "expectedTargets":[
                "XRL.World.Parts.Campfire|Preserve|System.Boolean",
                "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean"
              ]
            }
            """);

        var result = QudTestRunner.Run("qudtest:bindings", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Cases[0].Actual.Split('\n'), Is.EquivalentTo(new[]
            {
                "XRL.World.Parts.Campfire|Preserve|System.Boolean",
                "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean",
            }));
        });
    }

    [Test]
    public void Run_PatchBindingRecordsFailureWhenExpectedTargetsAreStale()
    {
        WriteFixture(
            "bindings-smoke.json",
            suite: "bindings",
            """
            {
              "id":"binding.stale-target",
              "route":"patch-binding",
              "patch":"QudJP.Patches.CookingRecipeDisplayNameTranslationPatch",
              "expectedTargets":["XRL.World.Skills.Cooking.CookingRecipe|OldDisplayName|System.String"]
            }
            """);

        var result = QudTestRunner.Run("qudtest:bindings", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].Expected, Is.EqualTo("XRL.World.Skills.Cooking.CookingRecipe|OldDisplayName|System.String"));
            Assert.That(result.Cases[0].Actual.Split('\n'), Is.EquivalentTo(new[]
            {
                "XRL.World.Skills.Cooking.CookingRecipe|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.AppleMatz|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.BoneBabka|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.CloacaSurprise|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.CrystalDelight|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.GoatAndSweetLeaf|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.HotandSpiny|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.MahLahSoup|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.MushroomCider|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.ThePorridge|GetDisplayName|System.String",
                "XRL.World.Skills.Cooking.TongueAndCheek|GetDisplayName|System.String",
            }));
            Assert.That(result.Cases[0].Diagnostic, Does.Contain("expected"));
        });
    }

    [Test]
    public void Run_PatchBindingRecordsFailureWhenPatchTypeIsMissing()
    {
        WriteFixture(
            "bindings-smoke.json",
            suite: "bindings",
            """
            {
              "id":"binding.missing-patch",
              "route":"patch-binding",
              "patch":"QudJP.Patches.DoesNotExistTranslationPatch",
              "expectedTargets":["XRL.World.Missing|Target|System.Void"]
            }
            """);

        var result = QudTestRunner.Run("qudtest:bindings", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Cases[0].Diagnostic, Does.Contain("patch type not found"));
        });
    }

    [Test]
    public void Run_AllPatchBindingsEnumeratesPatchTargetResolution()
    {
        var result = QudTestRunner.Run("qudtest:bindings-all", fixturesDirectory, "ja");

        Assert.Multiple(() =>
        {
            Assert.That(result.Suite, Is.EqualTo("bindings-all"));
            Assert.That(result.TotalCount, Is.GreaterThan(100));
            Assert.That(result.Cases.Any(static testCase =>
                testCase.Id == "binding-all.PopupAskStringTranslationPatch"
                && testCase.Passed
                && testCase.Actual.Contains("XRL.UI.Popup|AskString|", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Cases.Any(static testCase =>
                testCase.Id == "binding-all.GameSummaryScreenMenuBarsTranslationPatch"
                && testCase.Passed
                && testCase.Actual == "Qud.UI.GameSummaryScreen|UpdateMenuBars|System.Void"), Is.True);
            Assert.That(result.Cases.Any(static testCase =>
                testCase.Id == "binding-all.HistoricStringExpanderPatch"
                && testCase.Passed
                && testCase.Expected == "zero targets are explicitly allowed"
                && testCase.Diagnostic.Contains("intentionally disabled", StringComparison.Ordinal)), Is.True);
        });
    }

    private void WriteFixture(string fileName, string suite, string casesJson)
    {
        File.WriteAllText(
            Path.Combine(fixturesDirectory, fileName),
            $$"""
            {
              "schemaVersion": 1,
              "suite": "{{suite}}",
              "description": "{{suite}} smoke",
              "cases": [
                {{casesJson}}
              ]
            }
            """,
            System.Text.Encoding.UTF8);
    }
}
#endif
